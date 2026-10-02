using Snail.MCP.Blender.Adapters.AddOn;
using Snail.MCP.Blender.Application.Diagnostics;
using Snail.MCP.Blender.Application.Transfer;
using Snail.MCP.Blender.Configuration;
using Snail.MCP.Blender.Tools;
using Snail.MCP.Blender.Tools.Base;

namespace Snail.MCP.Blender.Tests.Tools;

public class DiagnosticsToolsTests
{
    private static ServerConfig Config() => new() { DataDirectory = Path.Combine(Path.GetTempPath(), "snail-blender-tests", Guid.NewGuid().ToString("N")) };

    private static ServerHealth Health(SilentMonitor monitor, BridgeTraffic traffic, ITunnel? tunnel = null) =>
        new(Config(), monitor, traffic, new AddOnFreshness(new AddOnPackager(Config()), NullLogger<AddOnFreshness>.Instance), tunnel ?? new FakeTunnel(), new LocalProject(Config()));

    private static string BundledDigest() => new AddOnPackager(Config()).Describe().Digest;

    /// <summary>Ping is answered from the add-on's socket thread, so a Blender whose dispatcher has died answers it like a healthy one. The
    /// liveness answer tells them apart by the pulse — but only while nothing is running, because a render holds the main thread on purpose.</summary>
    [Fact]
    public async Task Liveness_WhenTheQueueHasNotBeenDrained_SaysStalledRatherThanUp()
    {
        var stalled = await Health(Alive(pumpedSecondsAgo: ServerHealth.StalledAfterSeconds + 60, executing: null), new BridgeTraffic())
            .LivenessAsync(CancellationToken.None);
        var rendering = await Health(Alive(pumpedSecondsAgo: ServerHealth.StalledAfterSeconds + 60, executing: "render_animation"), new BridgeTraffic())
            .LivenessAsync(CancellationToken.None);
        var busy = await Health(Alive(pumpedSecondsAgo: 0.2, executing: null), new BridgeTraffic()).LivenessAsync(CancellationToken.None);
        var gone = await Health(new SilentMonitor().Failing(BridgeError.Unavailable("127.0.0.1", 9876, "no answer")), new BridgeTraffic())
            .LivenessAsync(CancellationToken.None);

        Assert.Equal("stalled", stalled["blender"]!.ToString());
        Assert.Equal("up", rendering["blender"]!.ToString());
        Assert.Equal("up", busy["blender"]!.ToString());
        Assert.Equal("down", gone["blender"]!.ToString());
    }

    private static SilentMonitor Alive(double pumpedSecondsAgo, string? executing) =>
        new SilentMonitor().Answering(new JsonObject
        {
            ["blender"] = "5.2.1",
            ["busy"] = executing is not null,
            ["executing"] = executing,
            ["pumped_s_ago"] = pumpedSecondsAgo,
            ["addon"] = new JsonObject { ["version"] = "0.1.0", ["digest"] = BundledDigest() },
        });

    [Fact]
    public async Task Diagnose_AddOnAnswers_ReportsBlenderReachableWithItsVersion()
    {
        var monitor = new SilentMonitor().Answering(new JsonObject { ["blender"] = "4.2.0", ["busy"] = false, ["queued"] = 0 });
        var tools = new DiagnosticsTools(Health(monitor, new BridgeTraffic()), new AddOnPackager(Config()));

        var report = JsonNode.Parse((await tools.DiagnoseAsync()).Text())!;

        Assert.True(report["ok"]!.GetValue<bool>());
        Assert.True(report["data"]!["blender"]!["reachable"]!.GetValue<bool>());
        Assert.Equal("4.2.0", report["data"]!["blender"]!["blender"]!.ToString());
        Assert.Equal(1, monitor.Pings);
    }

    [Fact]
    public async Task Diagnose_AddOnSilent_ReportsUnreachableWithTheInstallHint()
    {
        var monitor = new SilentMonitor().Failing(BridgeError.Unavailable("127.0.0.1", 9876, "refused"));
        var tools = new DiagnosticsTools(Health(monitor, new BridgeTraffic()), new AddOnPackager(Config()));

        var report = JsonNode.Parse((await tools.DiagnoseAsync()).Text())!;

        Assert.True(report["ok"]!.GetValue<bool>());
        Assert.False(report["data"]!["blender"]!["reachable"]!.GetValue<bool>());
        Assert.Contains("blender_install_addon", report["data"]!["blender"]!["hint"]!.ToString(), StringComparison.Ordinal);
    }

    /// <summary>Cancelling a call stops the waiting, not the work; a model that reads "busy" needs to be told which of the two it is looking at.</summary>
    [Fact]
    public async Task Diagnose_BlenderBusyWithARender_SaysItCannotBeInterrupted()
    {
        var monitor = new SilentMonitor().Answering(new JsonObject { ["blender"] = "5.2.1", ["busy"] = true, ["executing"] = "render_image" });
        var tools = new DiagnosticsTools(Health(monitor, new BridgeTraffic()), new AddOnPackager(Config()));

        var note = JsonNode.Parse((await tools.DiagnoseAsync()).Text())!["data"]!["blender"]!["note"]!.ToString();

        Assert.Contains("cannot be interrupted", note, StringComparison.Ordinal);
        Assert.Contains("blender_render_job", note, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Diagnose_BlenderBusyWithAnOrdinaryCommand_SaysOnlyThatCommandsQueue()
    {
        var monitor = new SilentMonitor().Answering(new JsonObject { ["blender"] = "5.2.1", ["busy"] = true, ["executing"] = "import_file" });
        var tools = new DiagnosticsTools(Health(monitor, new BridgeTraffic()), new AddOnPackager(Config()));

        var note = JsonNode.Parse((await tools.DiagnoseAsync()).Text())!["data"]!["blender"]!["note"]!.ToString();

        Assert.DoesNotContain("cannot be interrupted", note, StringComparison.Ordinal);
        Assert.Contains("queue behind it", note, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Diagnose_WithATunnelThatIsDown_ShowsWhy_AndHowToFixIt()
    {
        var tunnel = new FakeTunnel(new JsonObject { ["host"] = "artist@203.0.113.42", ["up"] = false, ["lastError"] = "Permission denied (publickey)." });
        var tools = new DiagnosticsTools(Health(new SilentMonitor(), new BridgeTraffic(), tunnel), new AddOnPackager(Config()));

        var report = JsonNode.Parse((await tools.DiagnoseAsync()).Text())!["data"]!["tunnel"]!;

        Assert.Equal("Permission denied (publickey).", report["lastError"]!.ToString());
        Assert.Contains("host key", report["hint"]!.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Diagnose_WithoutAProject_SaysWhereDownloadsGo_AndHowToHaveOne()
    {
        var tools = new DiagnosticsTools(Health(new SilentMonitor(), new BridgeTraffic()), new AddOnPackager(Config()));

        var project = JsonNode.Parse((await tools.DiagnoseAsync()).Text())!["data"]!["project"]!;

        Assert.Null(project["directory"]);
        Assert.Contains("SNAIL_MCP_BLENDER_PROJECT_DIRECTORY", project["note"]!.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Diagnose_OverHttp_NamesTheTransport_AndPointsAtOpeningAProject()
    {
        var config = new ServerConfig { Transport = Transport.Http, Http = { PublicUrl = "https://blender.example.com" } };
        var health = new ServerHealth(config, new SilentMonitor(), new BridgeTraffic(), new AddOnFreshness(new AddOnPackager(config), NullLogger<AddOnFreshness>.Instance), new FakeTunnel(), new LocalProject(config));

        var report = JsonNode.Parse((await new DiagnosticsTools(health, new AddOnPackager(config)).DiagnoseAsync()).Text())!["data"]!;

        Assert.Equal("http", report["transport"]!["kind"]!.ToString());
        Assert.Equal("https://blender.example.com", report["transport"]!["publicUrl"]!.ToString());
        Assert.Contains("blender_open_project", report["project"]!["note"]!.ToString(), StringComparison.Ordinal);
        Assert.False(report.AsObject().ContainsKey("idleTimeoutMinutes"));
    }

    [Fact]
    public async Task Diagnose_WithBlenderOnThisMachine_HasNoTunnelToReport()
    {
        var tools = new DiagnosticsTools(Health(new SilentMonitor(), new BridgeTraffic()), new AddOnPackager(Config()));

        var report = JsonNode.Parse((await tools.DiagnoseAsync()).Text())!["data"]!;

        Assert.Null(report["tunnel"]);
    }

    /// <summary>A caller that gave up is not a caller that stopped Blender; the count is the only trace left of work still running.</summary>
    [Fact]
    public async Task Diagnose_AfterACancelledCall_CountsItAsAbandoned()
    {
        var traffic = new BridgeTraffic();
        traffic.Abandon(BridgeCommands.RenderImage);
        var tools = new DiagnosticsTools(Health(new SilentMonitor().Answering(new JsonObject { ["blender"] = "5.2.1" }), traffic), new AddOnPackager(Config()));

        var report = JsonNode.Parse((await tools.DiagnoseAsync()).Text())!["data"]!["traffic"]!;

        Assert.Equal(1, report["abandoned"]!.GetValue<int>());
        Assert.Equal("render_image", report["lastAbandoned"]!["command"]!.ToString());
    }

    /// <summary>The hint after a timeout depends on what Blender was doing: a render goes on, an import that hit a modal dialog does not.</summary>
    [Fact]
    public void TimeoutHint_OnARender_SaysTheRenderContinues_AndOnAnythingElseKeepsTheOldAdvice()
    {
        var render = ToolResponse.From(BridgeReply.Failed(BridgeError.Timeout(BridgeCommands.RenderImage, TimeSpan.FromSeconds(300))));
        var ordinary = ToolResponse.From(BridgeReply.Failed(BridgeError.Timeout(BridgeCommands.SceneInfo, TimeSpan.FromSeconds(30))));

        Assert.Contains("cannot be interrupted from outside", render.Text(), StringComparison.Ordinal);
        Assert.Contains("blender_render_job", render.Text(), StringComparison.Ordinal);
        Assert.Contains("render_image", render.Text(), StringComparison.Ordinal);
        Assert.Contains("longer timeoutSeconds", ordinary.Text(), StringComparison.Ordinal);
        Assert.DoesNotContain("cannot be interrupted", ordinary.Text(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Diagnose_Always_ReportsTheTrafficAndWhetherATokenIsSet()
    {
        var monitor = new SilentMonitor().Answering(new JsonObject { ["blender"] = "5.2.1" });
        var traffic = new BridgeTraffic();
        traffic.Record(BridgeCommands.SceneInfo, TimeSpan.FromMilliseconds(12), null);
        traffic.Record(BridgeCommands.ObjectInfo, TimeSpan.FromMilliseconds(3), RecordedAnswers.Error("object_info for a name no object has"));
        var tools = new DiagnosticsTools(Health(monitor, traffic), new AddOnPackager(Config()));

        var report = JsonNode.Parse((await tools.DiagnoseAsync()).Text())!["data"]!;

        Assert.Equal("none", report["bridge"]!["token"]!.ToString());
        Assert.Equal(2, report["traffic"]!["requests"]!.GetValue<int>());
        Assert.Equal(1, report["traffic"]!["failuresByType"]!["NotFound"]!.GetValue<int>());
        Assert.Equal("object_info", report["traffic"]!["lastFailure"]!["command"]!.ToString());
    }

    /// <summary>The version cannot answer this: it stays put across bug fixes, so a stale install would report the same one.</summary>
    [Fact]
    public async Task Diagnose_AddOnOfTheSameBuild_ReportsItCurrent()
    {
        var monitor = new SilentMonitor().Answering(new JsonObject
        {
            ["blender"] = "5.2.1",
            ["addon"] = new JsonObject { ["version"] = "0.1.0", ["digest"] = BundledDigest(), ["commands"] = 139 },
        });
        var tools = new DiagnosticsTools(Health(monitor, new BridgeTraffic()), new AddOnPackager(Config()));

        var report = JsonNode.Parse((await tools.DiagnoseAsync()).Text())!["data"]!;

        Assert.Equal("current", report["addOn"]!["status"]!.ToString());
        Assert.Equal(BundledDigest(), report["addOn"]!["installed"]!["digest"]!.ToString());
        Assert.True(monitor.AskedFor!["commands"]!.GetValue<bool>());
    }

    /// <summary>An add-on ahead of this server is not behind: it answers everything this server knows, and refusing it would make every
    /// upgrade a lockstep. It is reported as differing, without the hint to reinstall the older build over it.</summary>
    [Fact]
    public async Task Diagnose_AddOnAheadOfTheServer_IsNotReportedAsOlder()
    {
        var monitor = new SilentMonitor().Answering(new JsonObject
        {
            ["blender"] = "5.2.1",
            ["addon"] = new JsonObject
            {
                ["version"] = "0.2.0",
                ["digest"] = "beefbeefbeef",
                ["protocol"] = AddOnProtocol.Version + 1,
                ["capabilities"] = new JsonArray([.. AddOnProtocol.Required.Select(name => JsonValue.Create(name)), JsonValue.Create("something-new")]),
                ["commands"] = 200,
            },
        });
        var tools = new DiagnosticsTools(Health(monitor, new BridgeTraffic()), new AddOnPackager(Config()));

        var report = JsonNode.Parse((await tools.DiagnoseAsync()).Text())!["data"]!["addOn"]!;

        Assert.Equal("differs", report["status"]!.ToString());
        Assert.False(report["protocol"]!["behind"]!.GetValue<bool>());
        Assert.Empty(report["protocol"]!["lacking"]!.AsArray());
    }

    [Fact]
    public async Task Diagnose_AddOnOlderThanTheServer_NamesTheCommandsItLacks()
    {
        var registered = BridgeCommands.All.Select(command => command.Name).Where(name => name != "mesh_bevel" && name != "render_job").Select(name => JsonValue.Create(name));
        var monitor = new SilentMonitor().Answering(new JsonObject
        {
            ["blender"] = "5.2.1",
            ["addon"] = new JsonObject { ["version"] = "0.1.0", ["digest"] = "0123456789ab", ["commands"] = 137, ["command_names"] = new JsonArray([.. registered]) },
        });
        var tools = new DiagnosticsTools(Health(monitor, new BridgeTraffic()), new AddOnPackager(Config()));

        var report = JsonNode.Parse((await tools.DiagnoseAsync()).Text())!["data"]!["addOn"]!;

        Assert.Equal("older", report["status"]!.ToString());
        Assert.Equal(AddOnProtocol.Version, report["protocol"]!["server"]!.GetValue<int>());
        Assert.Equal(0, report["protocol"]!["addOn"]!.GetValue<int>());
        Assert.Equal(AddOnProtocol.Required, report["protocol"]!["lacking"]!.AsArray().Select(name => name!.ToString()));
        Assert.Equal(["mesh_bevel", "render_job"], report["missing"]!.AsArray().Select(name => name!.ToString()));
        Assert.Empty(report["notInTheCatalog"]!.AsArray());
        Assert.Contains("blender_install_addon", report["hint"]!.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Diagnose_AddOnThatDoesNotReportItsBuild_LeavesTheStatusUnknown()
    {
        var monitor = new SilentMonitor().Answering(new JsonObject { ["blender"] = "5.2.1" });
        var tools = new DiagnosticsTools(Health(monitor, new BridgeTraffic()), new AddOnPackager(Config()));

        var report = JsonNode.Parse((await tools.DiagnoseAsync()).Text())!["data"]!["addOn"]!;

        Assert.Equal("unknown", report["status"]!.ToString());
        Assert.Equal(BundledDigest(), report["bundled"]!["digest"]!.ToString());
    }

    [Fact]
    public void UnknownCommand_CarriesTheStaleAddOnHint()
    {
        var response = JsonNode.Parse(ToolResponse.From(RecordedAnswers.Reply("a command of a name the add-on has never registered")).Text())!;

        Assert.Contains("blender_install_addon", response["hint"]!.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void ToolResponse_FromFailedReply_CarriesTheTypeAndTheHint()
    {
        var reply = RecordedAnswers.Reply("an operator that cannot run in this context");

        var result = ToolResponse.From(reply);
        var response = JsonNode.Parse(result.Text())!;

        Assert.True(result.Failed());
        Assert.False(response["ok"]!.GetValue<bool>());
        Assert.Contains("cannot run in the current context", response["error"]!.ToString(), StringComparison.Ordinal);
        Assert.Equal("PollFailed", response["data"]!["type"]!.ToString());
        Assert.Equal("OBJECT", response["data"]!["mode"]!.ToString());
        Assert.Contains("area=VIEW_3D", response["hint"]!.ToString(), StringComparison.Ordinal);
    }
}
