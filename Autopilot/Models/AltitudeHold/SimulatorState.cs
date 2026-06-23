using Microsoft.FlightSimulator.SimConnect;

namespace Autopilot.Models.AltitudeHold;

public sealed class SimulatorState
{
    public bool IsConnected { get; set; } = false;
    public bool SimulationRunning { get; set; } = false;
    public DateTimeOffset LastUpdatedUtc { get; set; } = DateTimeOffset.UtcNow;
    public AltitudeHoldState AltitudeHoldState { get; set; } = new AltitudeHoldState();
}

public sealed class AltitudeHoldState
{
    public bool IsActive { get; set; } = false;
    public double LastEpochTime { get; set; } = 0.0;
    public double DeltaTime { get; set; } = 0.0;
    public double TargetAltitude { get; set; } = 0.0;
    public double TargetRoll { get; set; } = 0.0;
    public double TargetClimbAngle { get; set; } = 0.0;
    public double Altitude { get; set; } = 0.0;
    public double VerticalSpeed { get; set; } = 0.0;
    public double Roll { get; set; } = 0.0;
    public double Airspeed { get; set; } = 0.0;
    public double ClimbAngle { get; set; } = 0.0;
    public double ThrottlePosition { get; set; } = 0.0;
    public double ElevatorTrimPosition { get; set; } = 0.0;
    public double AileronTrimPosition { get; set; } = 0.0;
    public double AltitudeError { get; set; } = 0.0;
    public double AirspeedError { get; set; } = 0.0;
    public IPID ElevatorTrimPID { get; set; } = new PID(500e-4, 80e-4, 300e-4, -1, 1, -1e2, 1e2, 0);
    public IPID AileronTrimPID { get; set; } = new PID(-6e-4, 0, -1e-4, -1, 1, -1e2, 1e2, 0);
}

public sealed class AltitudeHoldStateConstants
{
    public enum Definitions
    {
        Altitude,
        Roll,
        Airspeed,
        VerticalSpeed,
        Throttle,
        ElevatorTrim,
        AileronTrim
    }

    public enum Requests
    {
        Altitude,
        Roll,
        Airspeed,
        VerticalSpeed,
        Throttle,
        ElevatorTrim, 
        AileronTrim
    }

    public enum Events
    {
        Start,
        Stop,
        SetElevatorTrim,
        SetAileronTrim,
        SetThrottle,
    }

    public enum Groups
    {
        AltitudeHold = 1
    }

    public static (Enum, Enum, string, string, SIMCONNECT_DATATYPE, SIMCONNECT_PERIOD)[] DataDefinitions { get; set; } = new (Enum, Enum, string, string, SIMCONNECT_DATATYPE, SIMCONNECT_PERIOD)[]
    {
        (Requests.Altitude, Definitions.Altitude, "PLANE ALTITUDE", "feet", SIMCONNECT_DATATYPE.FLOAT64, SIMCONNECT_PERIOD.SIM_FRAME),
        (Requests.Roll, Definitions.Roll, "PLANE BANK DEGREES", "degrees", SIMCONNECT_DATATYPE.FLOAT64, SIMCONNECT_PERIOD.SIM_FRAME),
        (Requests.Airspeed, Definitions.Airspeed, "AIRSPEED INDICATED", "knots", SIMCONNECT_DATATYPE.FLOAT64, SIMCONNECT_PERIOD.SIM_FRAME),
        (Requests.VerticalSpeed, Definitions.VerticalSpeed, "VERTICAL SPEED", "feet per minute", SIMCONNECT_DATATYPE.FLOAT64, SIMCONNECT_PERIOD.SIM_FRAME),
        (Requests.Throttle, Definitions.Throttle, "GENERAL ENG THROTTLE LEVER POSITION:1", "percent", SIMCONNECT_DATATYPE.FLOAT64, SIMCONNECT_PERIOD.SECOND),
        (Requests.ElevatorTrim, Definitions.ElevatorTrim, "ELEVATOR TRIM POSITION", "degrees", SIMCONNECT_DATATYPE.FLOAT64, SIMCONNECT_PERIOD.SECOND),
        (Requests.AileronTrim, Definitions.AileronTrim, "AILERON TRIM POSITION", "degrees", SIMCONNECT_DATATYPE.FLOAT64, SIMCONNECT_PERIOD.SECOND)
    };
}