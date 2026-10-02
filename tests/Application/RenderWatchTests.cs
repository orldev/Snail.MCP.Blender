using ModelContextProtocol;
using Snail.MCP.Blender.Application.Rendering;

namespace Snail.MCP.Blender.Tests.Application;

/// <summary>A blocking render is invisible to the client unless the watch turns monitor probes into progress notifications.</summary>
public class RenderWatchTests
{
    [Fact]
    public async Task Follow_WithoutAProgressToken_NeverProbes()
    {
        var monitor = new SilentMonitor();
        var watch = new RenderWatch(monitor) { Interval = TimeSpan.FromMilliseconds(5) };

        var reply = await watch.FollowAsync(Task.Delay(30).ContinueWith(_ => BridgeReply.Ok(new JsonObject())), null, 1, CancellationToken.None);

        Assert.True(reply.IsOk);
        Assert.Equal(0, monitor.Probes);
    }

    [Fact]
    public async Task Follow_LongRender_ReportsFramesFromTheMonitor()
    {
        var monitor = new SilentMonitor()
            .Then(new JsonObject { ["phase"] = "rendering", ["frame"] = 3, ["frames_done"] = 2, ["elapsed_s"] = 4.5, ["stats"] = "Mem: 12M" })
            .Then(new JsonObject { ["phase"] = "rendering", ["frame"] = 4, ["frames_done"] = 3, ["elapsed_s"] = 6.0 });
        var watch = new RenderWatch(monitor) { Interval = TimeSpan.FromMilliseconds(10) };
        var reported = new List<ProgressNotificationValue>();
        var progress = new Progress<ProgressNotificationValue>(reported.Add);
        var render = new TaskCompletionSource<BridgeReply>();

        var following = watch.FollowAsync(render.Task, progress, 10, CancellationToken.None);
        await Task.Delay(80);
        render.SetResult(BridgeReply.Ok(new JsonObject { ["done"] = true }));
        var reply = await following;
        await Task.Delay(20);

        Assert.True(reply.IsOk);
        Assert.True(monitor.Probes >= 2);
        Assert.Contains(reported, value => value.Progress == 2 && value.Total == 10 && value.Message!.Contains("Mem: 12M", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Follow_MonitorUnreachable_StillReturnsTheReply()
    {
        var watch = new RenderWatch(new SilentMonitor()) { Interval = TimeSpan.FromMilliseconds(5) };
        var reported = 0;

        var reply = await watch.FollowAsync(Task.Delay(30).ContinueWith(_ => BridgeReply.Failed(new BridgeError("Timeout", "late"))), new Progress<ProgressNotificationValue>(_ => reported++), null, CancellationToken.None);

        Assert.False(reply.IsOk);
        Assert.Equal(0, reported);
    }
}
