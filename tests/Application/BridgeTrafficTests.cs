using Snail.MCP.Blender.Application.Diagnostics;

namespace Snail.MCP.Blender.Tests.Application;

public class BridgeTrafficTests
{
    /// <summary>The name in the report is the client the call was served for, carried from the session rather than passed in by hand.</summary>
    [Fact]
    public async Task TracedBridge_RecordsTheCommand_UnderTheClientItWasServing()
    {
        var traffic = new BridgeTraffic();
        var clients = Access.Sessions();
        var lighting = clients.Open(new ModelContextProtocol.Server.McpServerOptions(), "lighting");
        var bridge = new TracedBridge(new ScriptedBridge().Answer(BridgeCommands.SceneInfo, new JsonObject()), traffic, NullLogger<TracedBridge>.Instance, clients);

        using (clients.Serve(lighting))
        {
            await bridge.SendAsync(BridgeCommands.SceneInfo);
        }

        Assert.Equal("lighting", traffic.Report()["clients"]!.AsArray()[0]!["client"]!.ToString());
    }

    [Fact]
    public void Report_AfterMixedCalls_CountsRequestsFailuresAndTimingsPerCommand()
    {
        var traffic = new BridgeTraffic();

        traffic.Record(BridgeCommands.SceneInfo, TimeSpan.FromMilliseconds(10), null);
        traffic.Record(BridgeCommands.SceneInfo, TimeSpan.FromMilliseconds(30), null);
        traffic.Record(BridgeCommands.ObjectInfo, TimeSpan.FromMilliseconds(5), RecordedAnswers.Error("object_info for a name no object has"));
        traffic.Record(BridgeCommands.Python, TimeSpan.FromSeconds(2), new BridgeError(BridgeError.TimeoutType, "late"));

        var report = traffic.Report();
        var sceneInfo = report["commands"]!.AsArray().Single(entry => entry!["command"]!.ToString() == "scene_info")!;

        Assert.Equal(4, report["requests"]!.GetValue<int>());
        Assert.Equal(2, report["failures"]!.GetValue<int>());
        Assert.Equal(2, sceneInfo["count"]!.GetValue<int>());
        Assert.Equal(20, sceneInfo["averageMs"]!.GetValue<double>());
        Assert.Equal(30, sceneInfo["slowestMs"]!.GetValue<double>());
        Assert.Equal(1, report["failuresByType"]!["NotFound"]!.GetValue<int>());
        Assert.Equal("python", report["lastFailure"]!["command"]!.ToString());
    }

    [Fact]
    public void Report_BeforeAnyCall_IsEmptyButComplete()
    {
        var report = new BridgeTraffic().Report();

        Assert.Equal(0, report["requests"]!.GetValue<int>());
        Assert.Empty(report["commands"]!.AsArray());
        Assert.Null(report["lastFailure"]);
    }

    [Fact]
    public async Task TracedBridge_PassesTheReplyThrough_AndRecordsIt()
    {
        var inner = new ScriptedBridge()
            .Answer(BridgeCommands.SceneInfo, new JsonObject { ["name"] = "Scene" })
            .Fail(BridgeCommands.ObjectInfo, RecordedAnswers.Error("object_info for a name no object has"));
        var traffic = new BridgeTraffic();
        var bridge = new TracedBridge(inner, traffic, NullLogger<TracedBridge>.Instance);

        var answered = await bridge.SendAsync(BridgeCommands.SceneInfo, new JsonObject { ["scene"] = "Scene" }, TimeSpan.FromSeconds(9));
        var failed = await bridge.SendAsync(BridgeCommands.ObjectInfo);

        Assert.Equal("Scene", answered.Result!["name"]!.ToString());
        Assert.Equal("NotFound", failed.Error!.Type);
        Assert.Equal(TimeSpan.FromSeconds(9), inner.Sent[0].Timeout);
        Assert.Equal(2, traffic.Report()["requests"]!.GetValue<int>());
        Assert.Equal(1, traffic.Report()["failures"]!.GetValue<int>());
    }
}
