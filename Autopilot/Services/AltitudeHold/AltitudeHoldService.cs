using Autopilot.Models.AltitudeHold;
using Microsoft.FlightSimulator.SimConnect;
using System.Runtime.InteropServices;

namespace Autopilot.Services.AltitudeHold;

public sealed class AltitudeHoldService(ILogger<AltitudeHoldService> logger) : IAltitudeHoldService
{
    private readonly Lock _sync = new();
    private AutoResetEvent? _messageReceivedEvent;
    private SimConnect? _simConnect;
    const int WM_USER_SIMCONNECT = 0x0402;  // Copied from SimConnect documentation, TODO: Understand what this does. @Ben: what is the usage of this ID?
    private SimulatorState _currentState = new SimulatorState();

    public Task ConnectAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_sync)
        {
            if (_currentState.IsConnected)
            {
                return Task.CompletedTask;
            }

            // Get SimConnect ready by creating a new instance and setting up event handlers.
            try
            {
                _messageReceivedEvent = new AutoResetEvent(false);
                _simConnect = new SimConnect("Autopilot Service", 0, WM_USER_SIMCONNECT, _messageReceivedEvent, 0);
                _currentState.IsConnected = true;
                logger.LogInformation("SimConnect connected successfully.");

                // Get SimConnect ready by registering data definitions and requests and all that jazz
                SetupSimConnect();
            }
            catch (COMException ex)
            {
                // Logging as debug since usually this means MSFS is not running.
                logger.LogDebug(ex, "SimConnect connection failed with COMException.");
                logger.LogWarning("SimConnect connection failed.");
            }
        }

        return Task.CompletedTask;
    }

    public Task DisconnectAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_sync)
        {
            if (!_currentState.IsConnected)
            {
                return Task.CompletedTask;
            }

            // Dispose SimConnect instance and release resources.
            // @Ben: Is this the correct way to clean up SimConnect? I am not sure if I need to unsubscribe from events or do anything else.
            _simConnect?.Dispose();
            _simConnect = null;
            _messageReceivedEvent?.Dispose();
            _messageReceivedEvent = null;
            _currentState.IsConnected = false;
        }

        logger.LogInformation("SimConnect disconnected.");
        return Task.CompletedTask;
    }

    public Task<SimulatorState> GetStateAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_sync)
        {
            return Task.FromResult(_currentState);
        }
    }

    public Task ActivateAltitudeHoldAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_sync)
        {
            _currentState.AltitudeHoldState.IsActive = true;
            logger.LogInformation("Altitude hold activated.");

            // Reset PID controllers to avoid any accumulated error from previous runs.
            _currentState.AltitudeHoldState.ElevatorTrimPID.Reset();
            _currentState.AltitudeHoldState.AileronTrimPID.Reset();
        }
        return Task.CompletedTask;
    }

    public Task DeactivateAltitudeHoldAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_sync)
        {
            _currentState.AltitudeHoldState.IsActive = false;
            logger.LogInformation("Altitude hold deactivated.");
        }
        return Task.CompletedTask;
    }

    public Task SetTargetAltitudeAsync(double targetAltitude, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_sync)
        {
            _currentState.AltitudeHoldState.TargetAltitude = targetAltitude;
            logger.LogInformation("Target altitude set to {TargetAltitude} feet.", targetAltitude);
        }
        return Task.CompletedTask;
    }

    public Task SetPIDParametersAsync(string controllerName, double Kp, double Ki, double Kd, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_sync)
        {
            PID? pidController = controllerName.ToLower() switch
            {
                "elevatortrim" => _currentState.AltitudeHoldState.ElevatorTrimPID as PID,
                "aileronstrim" => _currentState.AltitudeHoldState.AileronTrimPID as PID,
                _ => null
            };
            
            if (pidController == null)
            {
                logger.LogWarning("Unknown controller name '{ControllerName}' for PID parameter update.", controllerName);
                return Task.CompletedTask;
            }

            // Reset the PID controller to avoid any accumulated error from previous runs.
            pidController?.Reset(); 

            // Apply the new PID parameters
            pidController?.Kp = Kp;
            pidController?.Ki = Ki;
            pidController?.Kd = Kd;
            logger.LogInformation("PID parameters for {ControllerName} updated: Kp={Kp}, Ki={Ki}, Kd={Kd}.", controllerName, Kp, Ki, Kd);

        }
        return Task.CompletedTask;
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                // Periodically check for SimConnect connection and run dispatch loop until cancellation is requested.
                while (!_currentState.IsConnected)
                {
                    logger.LogInformation("Waiting for SimConnect connection...");
                    await ConnectAsync(cancellationToken);
                    await Task.Delay(1000, cancellationToken);
                }

                if (_currentState.IsConnected)
                {
                    // @Ben: Is this the proper way to handle the message loop? The system is event driven but this feels like polling.
                    // Is it correct to assume that this if statement will remain true until all messages in the queue are processed?
                    // If so is there a risk of locking forever is the sim is sending events faster that we can process them for any reason?
                    if (_messageReceivedEvent?.WaitOne(50) == true)
                    {
                        _simConnect?.ReceiveMessage();
                    }

                    await Task.Delay(50, cancellationToken);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected during shutdown.
        }
        finally
        {
            await DisconnectAsync(CancellationToken.None);
        }
    }

    private void SetupSimConnect()
    {
        if (_simConnect == null)
        {
            logger.LogError("SimConnect is not initialized. Cannot set up data definitions and requests.");
            return;
        }

        // Attach system events
        _simConnect.OnRecvSimobjectData += OnRecvSimobjectData;
        _simConnect.OnRecvOpen += (s, e) =>
        {
            logger.LogInformation("SimConnect connection opened.");
            _currentState.IsConnected = true;
        };
        _simConnect.OnRecvQuit += (s, e) =>
        {
            logger.LogInformation("SimConnect connection closed by simulator.");
            _currentState.IsConnected = false;
        };
        _simConnect.OnRecvException += (s, e) =>
        {
            logger.LogError("SimConnect exception received: {Exception}", e.dwException);
        };

        // Listen for system events to track simulation state.
        _simConnect.OnRecvEvent += (s, e) =>
        {
            if ((AltitudeHoldStateConstants.Events)e.uEventID == AltitudeHoldStateConstants.Events.Start)
            {
                logger.LogInformation("Simulation started.");
                _currentState.SimulationRunning = true;
            }
            else if ((AltitudeHoldStateConstants.Events)e.uEventID == AltitudeHoldStateConstants.Events.Stop)
            {
                logger.LogInformation("Simulation stopped.");
                // Disable altitude hold when the simulation stops to avoid unexpected behavior.
                DeactivateAltitudeHoldAsync(CancellationToken.None).Wait();
                _currentState.SimulationRunning = false;
            }
        };

        // Data definitions
        foreach (var (request, definition, name, unit, datatype, period) in AltitudeHoldStateConstants.DataDefinitions)
        {
            _simConnect.AddToDataDefinition(definition, name, unit, datatype, 0.0f, 0u);
            _simConnect.RegisterDataDefineStruct<double>(definition);
            _simConnect.RequestDataOnSimObject(request, definition, SimConnect.SIMCONNECT_OBJECT_ID_USER, period, SIMCONNECT_DATA_REQUEST_FLAG.DEFAULT, 0u, 0u, 0u);
        }

        // System events
        _simConnect.SubscribeToSystemEvent(AltitudeHoldStateConstants.Events.Start, "SimStart");
        _simConnect.SubscribeToSystemEvent(AltitudeHoldStateConstants.Events.Stop, "SimStop");

        // Input events
        _simConnect.MapClientEventToSimEvent(AltitudeHoldStateConstants.Events.SetElevatorTrim, "ELEVATOR_TRIM_SET");
        _simConnect.MapClientEventToSimEvent(AltitudeHoldStateConstants.Events.SetAileronTrim, "AILERON_TRIM_SET");
        _simConnect.MapClientEventToSimEvent(AltitudeHoldStateConstants.Events.SetThrottle, "THROTTLE_SET"); // Not used yet

        // Register group and set priority for the events
        _simConnect.AddClientEventToNotificationGroup(AltitudeHoldStateConstants.Groups.AltitudeHold, AltitudeHoldStateConstants.Events.SetElevatorTrim, false);
        _simConnect.AddClientEventToNotificationGroup(AltitudeHoldStateConstants.Groups.AltitudeHold, AltitudeHoldStateConstants.Events.SetAileronTrim, false);
        _simConnect.AddClientEventToNotificationGroup(AltitudeHoldStateConstants.Groups.AltitudeHold, AltitudeHoldStateConstants.Events.SetThrottle, false); // Not used yet
        _simConnect.SetNotificationGroupPriority(AltitudeHoldStateConstants.Groups.AltitudeHold, SimConnect.SIMCONNECT_GROUP_PRIORITY_HIGHEST);

        logger.LogInformation("SimConnect data definitions and requests set up successfully.");
    }

    private void OnRecvSimobjectData(SimConnect sender, SIMCONNECT_RECV_SIMOBJECT_DATA data)
    {
        lock (_sync)
        {
            _currentState.LastUpdatedUtc = DateTimeOffset.UtcNow;

            // Update the appropriate state property based on the definition ID.
            switch ((AltitudeHoldStateConstants.Definitions)data.dwDefineID)
            {
                case AltitudeHoldStateConstants.Definitions.Altitude:
                    _currentState.AltitudeHoldState.Altitude = (double)data.dwData[0];
                    break;
                case AltitudeHoldStateConstants.Definitions.Roll:
                    _currentState.AltitudeHoldState.Roll = (double)data.dwData[0];
                    break;
                case AltitudeHoldStateConstants.Definitions.Airspeed:
                    _currentState.AltitudeHoldState.Airspeed = (double)data.dwData[0];
                    break;
                case AltitudeHoldStateConstants.Definitions.VerticalSpeed:
                    _currentState.AltitudeHoldState.VerticalSpeed = (double)data.dwData[0];
                    break;
                case AltitudeHoldStateConstants.Definitions.Throttle:
                    _currentState.AltitudeHoldState.ThrottlePosition = (double)data.dwData[0];
                    break;
                case AltitudeHoldStateConstants.Definitions.ElevatorTrim:
                    _currentState.AltitudeHoldState.ElevatorTrimPosition = (double)data.dwData[0];
                    break;
                case AltitudeHoldStateConstants.Definitions.AileronTrim:
                    _currentState.AltitudeHoldState.AileronTrimPosition = (double)data.dwData[0];
                    break;
            }

            // The main control loop is triggered by the Altitude data definition, which is updated at a regular interval.
            if ((AltitudeHoldStateConstants.Requests)data.dwRequestID == AltitudeHoldStateConstants.Requests.Altitude)
            {
                // Update the delta time for the PID loop
                // @Ben: right now I am using the system time to compute delta time. Can we get a more accurate delta time from the sim?
                double currentEpochTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0; // Convert milliseconds to seconds

                // If this is the first time we're receiving data, initialize LastEpochTime to avoid a large delta time.
                if (_currentState.AltitudeHoldState.LastEpochTime == 0.0)
                {
                    _currentState.AltitudeHoldState.LastEpochTime = currentEpochTime;
                }
                _currentState.AltitudeHoldState.DeltaTime = currentEpochTime - _currentState.AltitudeHoldState.LastEpochTime;
                _currentState.AltitudeHoldState.LastEpochTime = currentEpochTime;

                ControlLoop();  // <----- MAIN PID LOOP
            }
        }
    }

    private void ControlLoop()
    {
        // Compute the current climb angle based on vertical speed and airspeed.
        // I usually work in S.I. units and would keep this in radians, but degrees are more intuitive for tuning imho.
        _currentState.AltitudeHoldState.ClimbAngle = Converters.RadiansToDegrees(Math.Atan2(_currentState.AltitudeHoldState.VerticalSpeed, Converters.KnotsToFeetPerMinute(_currentState.AltitudeHoldState.Airspeed))); // Convert knots to feet per minute for climb angle calculation

        if (_currentState.AltitudeHoldState.IsActive)
        {
            double altitudeError = _currentState.AltitudeHoldState.TargetAltitude - _currentState.AltitudeHoldState.Altitude;

            // ============ Elevator trim Control ===========
            _currentState.AltitudeHoldState.AltitudeError = altitudeError;

            // Compute the desired climb/descent angle based on the altitude error
            // To smoothly intercept the target altitude use an arctangent function.
            double newClimbAngle = Converters.RadiansToDegrees(Math.Atan2(altitudeError, 1000));

            // If the new climb angle is small, set it faster to avoid overshooting.
            // If the new climb angle is larger, apply a smoothing factor to avoid abrupt changes. 
            // This helps avoiding passengers puke :D
            double smoothFactor = Math.Abs(newClimbAngle) < 1 ? 0.99 : 0.999;
            _currentState.AltitudeHoldState.TargetClimbAngle = smoothFactor * _currentState.AltitudeHoldState.TargetClimbAngle + (1-smoothFactor) * newClimbAngle;

            // Limit the angle to a reasonable range to avoid excessive control inputs and to avoid stall/overspeed.
            _currentState.AltitudeHoldState.TargetClimbAngle = Math.Clamp(_currentState.AltitudeHoldState.TargetClimbAngle, -5, 5);

            // Compute the climb angle error
            double climbAngleError = _currentState.AltitudeHoldState.TargetClimbAngle - _currentState.AltitudeHoldState.ClimbAngle;

            // Update PID to maintain the target climb angle
            _currentState.AltitudeHoldState.ElevatorTrimPID.Update(climbAngleError, _currentState.AltitudeHoldState.DeltaTime);
            double elevatorTrimOutput = _currentState.AltitudeHoldState.ElevatorTrimPID.ComputeOutput();

            // Scale the elevator trim with speed. More speed = less elevator trim. This is very rough, it would be better to know the correct trim value for the current speed.
            elevatorTrimOutput /= Math.Max(1.0, _currentState.AltitudeHoldState.Airspeed / 100.0);

            // Scale output to match SimConnect's expected range for control surfaces
            int normalizedElevatorTrimOutput = (int)(elevatorTrimOutput * 16384);

            // Use the ELEVATOR_TRIM_SET Event to set the elevator trim position
            // From what I can tell from the SDK, the value is expected to be in the range of -16384 to 16384. TransmitClientEvent expects a uint, so we need to cast it.
            // Ben: is this correct?
            _simConnect?.TransmitClientEvent(
                SimConnect.SIMCONNECT_OBJECT_ID_USER,
                AltitudeHoldStateConstants.Events.SetElevatorTrim,
                (uint)normalizedElevatorTrimOutput,
                AltitudeHoldStateConstants.Groups.AltitudeHold,
                SIMCONNECT_EVENT_FLAG.GROUPID_IS_PRIORITY
            );

            // ============ Aileron Trim Control ============
            // Aileron trim control (used to maintain wings level)
            double rollError = 0.0 - _currentState.AltitudeHoldState.Roll; // Assuming we want to maintain 0 degrees of roll
            _currentState.AltitudeHoldState.AileronTrimPID.Update(rollError, _currentState.AltitudeHoldState.DeltaTime);
            double aileronTrimOutput = _currentState.AltitudeHoldState.AileronTrimPID.ComputeOutput();

            // Scale output to match SimConnect's expected range for control surfaces
            int normalizedAileronTrimOutput = (int)(aileronTrimOutput * 16384);

            // Use the AILERON_TRIM_SET Event to set the aileron trim position
            // From what I can tell from the SDK, the value is expected to be in the range of -16384 to 16384. TransmitClientEvent expects a uint, so we need to cast it.
            _simConnect?.TransmitClientEvent(
                SimConnect.SIMCONNECT_OBJECT_ID_USER,
                AltitudeHoldStateConstants.Events.SetAileronTrim,
                (uint)normalizedAileronTrimOutput,
                AltitudeHoldStateConstants.Groups.AltitudeHold,
                SIMCONNECT_EVENT_FLAG.GROUPID_IS_PRIORITY
            );
        }
        else
        {
            // If the altitude hold is not active, we should reset the target climb angle to the current climb angle to avoid sudden jumps when reactivating.
            _currentState.AltitudeHoldState.TargetClimbAngle = _currentState.AltitudeHoldState.ClimbAngle;
        }
    }
}
