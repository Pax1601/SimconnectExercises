using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Autopilot.Models;
using Autopilot.Services.AltitudeHold;

namespace Autopilot.Controllers;

public class HomeController(IAltitudeHoldService simConnectService) : Controller
{

    public IActionResult Index()
    {
        return View();
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [HttpGet]
    public async Task<IActionResult> SimConnectStatus(CancellationToken cancellationToken)
    {
        var state = await simConnectService.GetStateAsync(cancellationToken);

        return Json(new
        {
            isConnected = state.IsConnected,
            simulationRunning = state.SimulationRunning,
            lastUpdatedUtc = state.LastUpdatedUtc
        });
    }

    [HttpGet]
    public async Task<IActionResult> AltitudeHoldState(CancellationToken cancellationToken)
    {
        var state = await simConnectService.GetStateAsync(cancellationToken);

        return Json(new
        {
            altitude = state.AltitudeHoldState.Altitude,
            airspeed = state.AltitudeHoldState.Airspeed,
            verticalSpeed = state.AltitudeHoldState.VerticalSpeed,
            climbAngle = state.AltitudeHoldState.ClimbAngle,
            roll = state.AltitudeHoldState.Roll,
            throttlePosition = state.AltitudeHoldState.ThrottlePosition,
            elevatorTrimPosition = state.AltitudeHoldState.ElevatorTrimPosition,
            aileronTrimPosition = state.AltitudeHoldState.AileronTrimPosition,
            isActive = state.AltitudeHoldState.IsActive,
            targetAltitude = state.AltitudeHoldState.TargetAltitude,
            targetClimbAngle = state.AltitudeHoldState.TargetClimbAngle,
            lastUpdatedUtc = state.LastUpdatedUtc,
            elevatorTrimPID = new
            {
                state.AltitudeHoldState.ElevatorTrimPID.Kp,
                state.AltitudeHoldState.ElevatorTrimPID.Ki,
                state.AltitudeHoldState.ElevatorTrimPID.Kd,
                state.AltitudeHoldState.ElevatorTrimPID.ProportionalTerm,
                state.AltitudeHoldState.ElevatorTrimPID.IntegralTerm,
                state.AltitudeHoldState.ElevatorTrimPID.DerivativeTerm,
            },
            aileronTrimPID = new
            {
                state.AltitudeHoldState.AileronTrimPID.Kp,
                state.AltitudeHoldState.AileronTrimPID.Ki,
                state.AltitudeHoldState.AileronTrimPID.Kd,
                state.AltitudeHoldState.AileronTrimPID.ProportionalTerm,
                state.AltitudeHoldState.AileronTrimPID.IntegralTerm,
                state.AltitudeHoldState.AileronTrimPID.DerivativeTerm,
            }
        });
    }

    [HttpPost]
    public async Task<IActionResult> ActivateAltitudeHold(CancellationToken cancellationToken)
    {
        await simConnectService.ActivateAltitudeHoldAsync(cancellationToken);
        return NoContent();
    }

    [HttpPost]
    public async Task<IActionResult> DeactivateAltitudeHold(CancellationToken cancellationToken)
    {
        await simConnectService.DeactivateAltitudeHoldAsync(cancellationToken);
        return NoContent();
    }

    [HttpPost]
    public async Task<IActionResult> SetTargetAltitude([FromQuery] double targetAltitude, CancellationToken cancellationToken)
    {
        await simConnectService.SetTargetAltitudeAsync(targetAltitude, cancellationToken);
        return NoContent();
    }

    [HttpPost]
    public async Task<IActionResult> SetPIDParameters([FromQuery] string controllerName, [FromQuery] double Kp, [FromQuery] double Ki, [FromQuery] double Kd, CancellationToken cancellationToken)
    {
        await simConnectService.SetPIDParametersAsync(controllerName, Kp, Ki, Kd, cancellationToken);
        return NoContent();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
