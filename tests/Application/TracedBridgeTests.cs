using Snail.MCP.Blender.Application.Diagnostics;

namespace Snail.MCP.Blender.Tests.Application;

/// <summary>The stopwatch around the link: what the traffic report learns from an exchange that answered, one that failed, and one the caller gave up on.</summary>
public class TracedBridgeTests
{
    /// <summary>A link that never answers: the command is still on Blender's main thread when the caller walks away, and only the server can say so.</summary>
    private sealed class Silent : IBlenderBridge
    {
        public bool IsConnected { get; init; }

        public async Task<BridgeReply> SendAsync(BridgeCommand command, JsonObject? parameters = null, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);

            return BridgeReply.Ok(new JsonObject());
        }
    }

    [Fact]
    public async Task ACommandTheCallerGaveUpOn_IsCountedAsAbandoned_AndTheCancellationReachesTheCaller()
    {
        var traffic = new BridgeTraffic();
        var bridge = new TracedBridge(new Silent(), traffic, NullLogger<TracedBridge>.Instance);
        using var cancelling = new CancellationTokenSource();

        await cancelling.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => bridge.SendAsync(BridgeCommands.RenderImage, cancellationToken: cancelling.Token));

        var report = traffic.Report();

        Assert.Equal(1, report["abandoned"]!.GetValue<int>());
        Assert.Equal("render_image", report["lastAbandoned"]!["command"]!.ToString());
        Assert.Equal(0, report["requests"]!.GetValue<int>());
    }

    /// <summary>An add-on that does not know the command is an add-on older than the server, which the report counts apart from a command that merely failed.</summary>
    [Fact]
    public async Task ACommandTheAddOnDoesNotKnow_IsRecordedUnderItsOwnKindOfFailure()
    {
        var traffic = new BridgeTraffic();
        var link = new ScriptedBridge()
            .Fail(BridgeCommands.Scenes, RecordedAnswers.Error("a command of a name the add-on has never registered"))
            .Fail(BridgeCommands.ObjectInfo, RecordedAnswers.Error("object_info for a name no object has"));
        var bridge = new TracedBridge(link, traffic, NullLogger<TracedBridge>.Instance);

        await bridge.SendAsync(BridgeCommands.Scenes);
        await bridge.SendAsync(BridgeCommands.ObjectInfo, new JsonObject { ["name"] = "Cube" });

        var report = traffic.Report();

        Assert.Equal(2, report["requests"]!.GetValue<int>());
        Assert.Equal(2, report["failures"]!.GetValue<int>());
        Assert.Equal("NotFound", report["lastFailure"]!["type"]!.ToString());
    }

    [Fact]
    public void WhetherTheLinkIsUp_IsTheLinksAnswer_NotTheTracesOwn()
    {
        var traffic = new BridgeTraffic();

        Assert.True(new TracedBridge(new Silent { IsConnected = true }, traffic, NullLogger<TracedBridge>.Instance).IsConnected);
        Assert.False(new TracedBridge(new Silent(), traffic, NullLogger<TracedBridge>.Instance).IsConnected);
    }
}
