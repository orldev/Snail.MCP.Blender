using ModelContextProtocol;

namespace Snail.MCP.Blender.Application.Rendering;

/// <summary>Follows a render that blocks the control link: polls the monitor link while the reply is on its way and reports each step as MCP progress.</summary>
/// <remarks>Without a progress token from the client there is nothing to report to, so the reply is awaited as it is. The poll
/// interval is long enough that a probe never competes with the render for Blender's attention; the timer stops the moment
/// the render ends, so no probe is left pending after the reply.</remarks>
public sealed class RenderWatch(IBlenderMonitor monitor)
{
    public TimeSpan Interval { get; init; } = TimeSpan.FromSeconds(2);

    public async Task<BridgeReply> FollowAsync(Task<BridgeReply> render, IProgress<ProgressNotificationValue>? progress, int? totalFrames, CancellationToken cancellationToken)
    {
        if (progress is null)
        {
            return await render.ConfigureAwait(false);
        }

        using var untilDone = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        using var ticks = new PeriodicTimer(Interval);
        var watching = ReportUntilCancelledAsync(ticks, progress, totalFrames, untilDone.Token);

        try
        {
            return await render.ConfigureAwait(false);
        }
        finally
        {
            await untilDone.CancelAsync().ConfigureAwait(false);
            await watching.ConfigureAwait(false);
        }
    }

    private async Task ReportUntilCancelledAsync(PeriodicTimer ticks, IProgress<ProgressNotificationValue> progress, int? totalFrames, CancellationToken cancellationToken)
    {
        try
        {
            while (await ticks.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
            {
                var probe = await monitor.ProbeAsync(cancellationToken).ConfigureAwait(false);

                if (probe.IsOk && probe.Result is JsonObject state)
                {
                    progress.Report(Describe(state, totalFrames));
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private static ProgressNotificationValue Describe(JsonObject state, int? totalFrames)
    {
        var done = state["frames_done"]?.GetValue<int>() ?? 0;
        var frame = state["frame"]?.GetValue<int?>();
        var phase = state["phase"]?.GetValue<string>() ?? "waiting";
        var stats = state["stats"]?.GetValue<string>();
        var elapsed = state["elapsed_s"]?.GetValue<double>() ?? 0;

        return new ProgressNotificationValue
        {
            Progress = done,
            Total = totalFrames,
            Message = $"{phase}, frame {frame?.ToString() ?? "-"}, {done} written, {elapsed:0}s{(stats is null ? string.Empty : $": {stats}")}",
        };
    }
}
