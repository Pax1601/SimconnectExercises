using Autopilot.Models.AltitudeHold;

namespace Autopilot.Services.AltitudeHold;

public interface IAltitudeHoldService
{
    Task ConnectAsync(CancellationToken cancellationToken);
    Task DisconnectAsync(CancellationToken cancellationToken);
    Task<SimulatorState> GetStateAsync(CancellationToken cancellationToken);
    Task ActivateAltitudeHoldAsync(CancellationToken cancellationToken);
    Task DeactivateAltitudeHoldAsync(CancellationToken cancellationToken);
    Task SetTargetAltitudeAsync(double targetAltitude, CancellationToken cancellationToken);
    Task SetPIDParametersAsync(string controllerName, double kp, double ki, double kd, CancellationToken cancellationToken);
    Task RunAsync(CancellationToken cancellationToken);
    
}
