using Autopilot.Models.AltitudeHold;
using Microsoft.FlightSimulator.SimConnect;
using System.Runtime.InteropServices;

namespace Autopilot.Services.AltitudeHold;

public sealed class AltitudeHoldService(ILogger<AltitudeHoldService> logger) : IAltitudeHoldService
{
    private readonly Lock _sync = new();
    private AutoResetEvent? _messageReceivedEvent;
    private SimConnect? _simConnect;
    const int WM_USER_SIMCONNECT = 0x0402;  // Copied from SimConnect documentation, TODO: Understand what this does.
    private SimulatorState _currentState = new SimulatorState();

    public bool IsConnected
    {
        get
        {
            lock (_sync)
            {
                return _currentState.IsConnected;
            }
        }
    }

    public Task ConnectAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_sync)
        {
            if (_currentState.IsConnected)
            {
                return Task.CompletedTask;
            }

            try
            {
                _messageReceivedEvent = new AutoResetEvent(false);
                _simConnect = new SimConnect("Autopilot Exercise", 0, WM_USER_SIMCONNECT, _messageReceivedEvent, 0);
                _currentState.IsConnected = true;
                logger.LogInformation("SimConnect connected successfully.");

                // Event handlers must be attached before requests start producing data.
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
                    if ((AltitudeHoldState.Events)e.uEventID == AltitudeHoldState.Events.Start)
                    {
                        logger.LogInformation("Simulation started.");
                        _currentState.SimulationRunning = true;
                    }
                    else if ((AltitudeHoldState.Events)e.uEventID == AltitudeHoldState.Events.Stop)
                    {
                        logger.LogInformation("Simulation stopped.");
                        // Disable altitude hold when the simulation stops to avoid unexpected behavior.
                        DeactivateAltitudeHoldAsync(CancellationToken.None).Wait();
                        _currentState.SimulationRunning = false;
                    }
                };

                // Data definitions
                _simConnect.AddToDataDefinition(
                    AltitudeHoldState.Definitions.Altitude,
                    "PLANE ALTITUDE",
                    "feet",
                    SIMCONNECT_DATATYPE.FLOAT64,
                    0.0f,
                    0u);
                _simConnect.AddToDataDefinition(
                AltitudeHoldState.Definitions.Roll,
                    "PLANE BANK DEGREES",
                    "degrees",
                    SIMCONNECT_DATATYPE.FLOAT64,
                    0.0f,
                    0u);
                _simConnect.AddToDataDefinition(
                    AltitudeHoldState.Definitions.Airspeed,
                    "AIRSPEED INDICATED",
                    "knots",
                    SIMCONNECT_DATATYPE.FLOAT64,
                    0.0f,
                    0u);
                _simConnect.AddToDataDefinition(
                    AltitudeHoldState.Definitions.VerticalSpeed,
                    "VERTICAL SPEED",
                    "feet per minute",
                    SIMCONNECT_DATATYPE.FLOAT64,
                    0.0f,
                    0u);
                _simConnect.AddToDataDefinition(
                    AltitudeHoldState.Definitions.Throttle,
                    "GENERAL ENG THROTTLE LEVER POSITION:1",
                    "percent",
                    SIMCONNECT_DATATYPE.FLOAT64,
                    0.0f,
                    0u);
                _simConnect.AddToDataDefinition(
                    AltitudeHoldState.Definitions.ElevatorTrim,
                    "ELEVATOR TRIM POSITION",
                    "degrees",
                    SIMCONNECT_DATATYPE.FLOAT64,
                    0.0f,
                    0u);
                _simConnect.AddToDataDefinition(
                    AltitudeHoldState.Definitions.AileronTrim,
                    "AILERON TRIM POSITION",
                    "degrees",
                    SIMCONNECT_DATATYPE.FLOAT64,
                    0.0f,
                    0u);
                _simConnect.RegisterDataDefineStruct<double>(AltitudeHoldState.Definitions.Altitude);
                _simConnect.RegisterDataDefineStruct<double>(AltitudeHoldState.Definitions.Roll);
                _simConnect.RegisterDataDefineStruct<double>(AltitudeHoldState.Definitions.Airspeed);
                _simConnect.RegisterDataDefineStruct<double>(AltitudeHoldState.Definitions.VerticalSpeed);
                _simConnect.RegisterDataDefineStruct<double>(AltitudeHoldState.Definitions.Throttle);
                _simConnect.RegisterDataDefineStruct<double>(AltitudeHoldState.Definitions.ElevatorTrim);
                _simConnect.RegisterDataDefineStruct<double>(AltitudeHoldState.Definitions.AileronTrim);

                // Data requests
                // Note: Altitude and roll are requested every frame. The main PID loop is tied to this frequency. TODO: Are there better approaches?
                // Other values are requested every second, since we will enforce them, so it is more of a "sanity check" than anything else.
                _simConnect.RequestDataOnSimObject(
                    AltitudeHoldState.Requests.Altitude,
                    AltitudeHoldState.Definitions.Altitude,
                    SimConnect.SIMCONNECT_OBJECT_ID_USER,
                    SIMCONNECT_PERIOD.SIM_FRAME,
                    SIMCONNECT_DATA_REQUEST_FLAG.DEFAULT,
                     0u,
                     0u,
                     0u
                    );
                _simConnect.RequestDataOnSimObject(
                    AltitudeHoldState.Requests.Roll,
                    AltitudeHoldState.Definitions.Roll,
                    SimConnect.SIMCONNECT_OBJECT_ID_USER,
                    SIMCONNECT_PERIOD.SIM_FRAME,
                    SIMCONNECT_DATA_REQUEST_FLAG.DEFAULT,
                     0u,
                     0u,
                     0u
                    );
                _simConnect.RequestDataOnSimObject(
                    AltitudeHoldState.Requests.Airspeed,
                    AltitudeHoldState.Definitions.Airspeed,
                    SimConnect.SIMCONNECT_OBJECT_ID_USER,
                    SIMCONNECT_PERIOD.SIM_FRAME,
                    SIMCONNECT_DATA_REQUEST_FLAG.DEFAULT,
                     0u,
                     0u,
                     0u
                    );
                _simConnect.RequestDataOnSimObject(
                    AltitudeHoldState.Requests.VerticalSpeed,
                    AltitudeHoldState.Definitions.VerticalSpeed,
                    SimConnect.SIMCONNECT_OBJECT_ID_USER,
                    SIMCONNECT_PERIOD.SIM_FRAME,
                    SIMCONNECT_DATA_REQUEST_FLAG.DEFAULT,
                     0u,
                     0u,
                     0u
                    );
                _simConnect.RequestDataOnSimObject(
                    AltitudeHoldState.Requests.Throttle,
                    AltitudeHoldState.Definitions.Throttle,
                    SimConnect.SIMCONNECT_OBJECT_ID_USER,
                    SIMCONNECT_PERIOD.SECOND,
                    SIMCONNECT_DATA_REQUEST_FLAG.DEFAULT,
                     0u,
                     0u,
                     0u
                    );
                _simConnect.RequestDataOnSimObject(
                    AltitudeHoldState.Requests.ElevatorTrim,
                    AltitudeHoldState.Definitions.ElevatorTrim,
                    SimConnect.SIMCONNECT_OBJECT_ID_USER,
                    SIMCONNECT_PERIOD.SECOND,
                    SIMCONNECT_DATA_REQUEST_FLAG.DEFAULT,
                     0u,
                     0u,
                     0u
                    );
                _simConnect.RequestDataOnSimObject(
                    AltitudeHoldState.Requests.AileronTrim,
                    AltitudeHoldState.Definitions.AileronTrim,
                    SimConnect.SIMCONNECT_OBJECT_ID_USER,
                    SIMCONNECT_PERIOD.SECOND,
                    SIMCONNECT_DATA_REQUEST_FLAG.DEFAULT,
                     0u,
                     0u,
                     0u
                    );

                // System events
                _simConnect.SubscribeToSystemEvent(AltitudeHoldState.Events.Start, "SimStart");
                _simConnect.SubscribeToSystemEvent(AltitudeHoldState.Events.Stop, "SimStop");

                // Input events
                _simConnect.MapClientEventToSimEvent(AltitudeHoldState.Events.SetElevatorTrim, "ELEVATOR_TRIM_SET");
                _simConnect.MapClientEventToSimEvent(AltitudeHoldState.Events.SetAileronTrim, "AILERON_TRIM_SET");
                _simConnect.MapClientEventToSimEvent(AltitudeHoldState.Events.SetThrottle, "THROTTLE_SET");

                // Register group and set priority for the events
                _simConnect.AddClientEventToNotificationGroup(AltitudeHoldState.Groups.AltitudeHold, AltitudeHoldState.Events.SetElevatorTrim, false);
                _simConnect.AddClientEventToNotificationGroup(AltitudeHoldState.Groups.AltitudeHold, AltitudeHoldState.Events.SetAileronTrim, false);
                _simConnect.AddClientEventToNotificationGroup(AltitudeHoldState.Groups.AltitudeHold, AltitudeHoldState.Events.SetThrottle, false);
                _simConnect.SetNotificationGroupPriority(AltitudeHoldState.Groups.AltitudeHold, SimConnect.SIMCONNECT_GROUP_PRIORITY_HIGHEST);

                // Initialize the PID controllers with default values. These can be adjusted later via the web interface.
                _currentState.AltitudeHoldState.ElevatorTrimPID.Kp = 500e-4;
                _currentState.AltitudeHoldState.ElevatorTrimPID.Ki = 80e-4;
                _currentState.AltitudeHoldState.ElevatorTrimPID.Kd = 300e-4;
                _currentState.AltitudeHoldState.ElevatorTrimPID.AntiWindupMin = -1e2;
                _currentState.AltitudeHoldState.ElevatorTrimPID.AntiWindupMax = 1e2;

                _currentState.AltitudeHoldState.AileronTrimPID.Kp = -6e-4;
                _currentState.AltitudeHoldState.AileronTrimPID.Ki = 0e-4;
                _currentState.AltitudeHoldState.AileronTrimPID.Kd = -1e-4;

                logger.LogInformation("SimConnect data definitions and requests set up successfully.");
            }
            catch (COMException ex)
            {
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

            if (controllerName.Equals("elevatorTrim", StringComparison.OrdinalIgnoreCase))
            {
                _currentState.AltitudeHoldState.ElevatorTrimPID.Kp = Kp;
                _currentState.AltitudeHoldState.ElevatorTrimPID.Ki = Ki;
                _currentState.AltitudeHoldState.ElevatorTrimPID.Kd = Kd;
                logger.LogInformation("Elevator PID parameters updated: Kp={Kp}, Ki={Ki}, Kd={Kd}", Kp, Ki, Kd);
            }
            else if (controllerName.Equals("aileronTrim", StringComparison.OrdinalIgnoreCase))
            {
                _currentState.AltitudeHoldState.AileronTrimPID.Kp = Kp;
                _currentState.AltitudeHoldState.AileronTrimPID.Ki = Ki;
                _currentState.AltitudeHoldState.AileronTrimPID.Kd = Kd;
                logger.LogInformation("Aileron PID parameters updated: Kp={Kp}, Ki={Ki}, Kd={Kd}", Kp, Ki, Kd);
            }
            else
            {
                logger.LogWarning("Unknown controller name '{ControllerName}' for PID parameter update.", controllerName);
            }

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
                while (!IsConnected)
                {
                    logger.LogInformation("Waiting for SimConnect connection...");
                    await ConnectAsync(cancellationToken);
                    await Task.Delay(1000, cancellationToken);
                }

                if (IsConnected)
                {
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

    private void OnRecvSimobjectData(SimConnect sender, SIMCONNECT_RECV_SIMOBJECT_DATA data)
    {
        lock (_sync)
        {
            _currentState.LastUpdatedUtc = DateTimeOffset.UtcNow;

            // Update the appropriate state property based on the request ID.
            if ((AltitudeHoldState.Requests)data.dwRequestID == AltitudeHoldState.Requests.Altitude)
            {
                var altitude = (double)data.dwData[0];
                _currentState.AltitudeHoldState.Altitude = altitude;

                // Update the delta time for the PID loop
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
            if ((AltitudeHoldState.Requests)data.dwRequestID == AltitudeHoldState.Requests.Roll)
            {
                var roll = (double)data.dwData[0];
                _currentState.AltitudeHoldState.Roll = roll;
            }
            if ((AltitudeHoldState.Requests)data.dwRequestID == AltitudeHoldState.Requests.Airspeed)
            {
                var airspeed = (double)data.dwData[0];
                _currentState.AltitudeHoldState.Airspeed = airspeed;
            }
            if ((AltitudeHoldState.Requests)data.dwRequestID == AltitudeHoldState.Requests.VerticalSpeed)
            {
                var verticalSpeed = (double)data.dwData[0];
                _currentState.AltitudeHoldState.VerticalSpeed = verticalSpeed;
            }
            else if ((AltitudeHoldState.Requests)data.dwRequestID == AltitudeHoldState.Requests.Throttle)
            {
                var throttle = (double)data.dwData[0];
                _currentState.AltitudeHoldState.ThrottlePosition = throttle;
            }
            else if ((AltitudeHoldState.Requests)data.dwRequestID == AltitudeHoldState.Requests.ElevatorTrim)
            {
                var elevatorTrim = (double)data.dwData[0];
                _currentState.AltitudeHoldState.ElevatorTrimPosition = elevatorTrim;
            }
            else if ((AltitudeHoldState.Requests)data.dwRequestID == AltitudeHoldState.Requests.AileronTrim)
            {
                var aileronTrim = (double)data.dwData[0];
                _currentState.AltitudeHoldState.AileronTrimPosition = aileronTrim;
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
            // Simple proportional mapping of altitude error to climb angle.
            double newClimbAngle = Converters.RadiansToDegrees(Math.Atan2(altitudeError, 1000));

            double smoothFactor; 
            if (Math.Abs(newClimbAngle) < 1) 
            {
                // If the new climb angle is small, set it faster to avoid overshooting.
                smoothFactor = 0.99;
            }
            else
            {
                // If the new climb angle is larger, apply a smoothing factor to avoid abrupt changes. 
                // This helps avoiding passengers puke :D
                smoothFactor = 0.999;
            }

            _currentState.AltitudeHoldState.TargetClimbAngle = smoothFactor * _currentState.AltitudeHoldState.TargetClimbAngle + (1-smoothFactor) * newClimbAngle;

            // Limit the angle to a reasonable range to avoid excessive control inputs
            _currentState.AltitudeHoldState.TargetClimbAngle = Math.Clamp(_currentState.AltitudeHoldState.TargetClimbAngle, -5, 5);
            // Compute the climb angle error
            double climbAngleError = _currentState.AltitudeHoldState.TargetClimbAngle - _currentState.AltitudeHoldState.ClimbAngle;

            // Update PID to maintain altitude
            _currentState.AltitudeHoldState.ElevatorTrimPID.Update(climbAngleError, _currentState.AltitudeHoldState.DeltaTime);
            double elevatorTrimOutput = _currentState.AltitudeHoldState.ElevatorTrimPID.ComputeOutput();

            // Scale the elevator trim with speed. More speed = less elevator trim.
            elevatorTrimOutput /= Math.Max(1.0, _currentState.AltitudeHoldState.Airspeed / 100.0);

            // Scale output to match SimConnect's expected range for control surfaces
            int normalizedElevatorTrimOutput = (int)(elevatorTrimOutput * 16384);

            // Use the ELEVATOR_TRIM_SET Event to set the elevator trim position
            // From what I can tell from the SDK, the value is expected to be in the range of -16384 to 16384. TransmitClientEvent expects a uint, so we need to cast it.
            _simConnect?.TransmitClientEvent(
                SimConnect.SIMCONNECT_OBJECT_ID_USER,
                AltitudeHoldState.Events.SetElevatorTrim,
                (uint)normalizedElevatorTrimOutput,
                AltitudeHoldState.Groups.AltitudeHold,
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
                AltitudeHoldState.Events.SetAileronTrim,
                (uint)normalizedAileronTrimOutput,
                AltitudeHoldState.Groups.AltitudeHold,
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
