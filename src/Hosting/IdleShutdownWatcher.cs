using Microsoft.Extensions.Hosting;

namespace Snail.MCP.Blender.Hosting;

/// <summary>Stops the host when no MCP tool has been called for longer than the idle timeout.</summary>
/// <remarks>A stdio server outlives a client that crashed without closing the pipe; without the watchdog every such
/// crash leaves a process behind until the machine reboots.</remarks>
public sealed class IdleShutdownWatcher(
    IActivityTracker activity,
    IdleShutdownOptions options,
    IHostApplicationLifetime appLifetime,
    TimeProvider timeProvider,
    ILogger<IdleShutdownWatcher> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (options.IdleTimeout <= TimeSpan.Zero)
        {
            return;
        }

        using var timer = new PeriodicTimer(options.PollInterval, timeProvider);

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                var idleFor = timeProvider.GetUtcNow().UtcDateTime - activity.LastActivityUtc;

                if (idleFor < options.IdleTimeout) continue;

                logger.LogWarning("No MCP tool has been called for {IdleFor}; the process looks orphaned, shutting down", idleFor);
                appLifetime.StopApplication();

                return;
            }
        }
        catch (OperationCanceledException)
        {
        }
    }
}
