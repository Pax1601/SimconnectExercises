namespace Autopilot.Services.AltitudeHold;

public sealed class AltitudeHoldHostedService(
    IAltitudeHoldService altitudeHoldService,
    ILogger<AltitudeHoldHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("AltitudeHold hosted service starting.");

        try
        {
            await altitudeHoldService.RunAsync(stoppingToken);
        }
        catch (OperationCanceledException)
        {
            // Expected when the service is stopping, no action needed.
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "AltitudeHold hosted service failed.");
            throw;
        }
        finally
        {
            logger.LogInformation("AltitudeHold hosted service stopping.");
        }
    }
}
