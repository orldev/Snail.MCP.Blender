using Microsoft.Extensions.DependencyInjection;
using Snail.MCP.Blender.Application.Diagnostics;
using Snail.MCP.Blender.Application.Sessions;
using Snail.MCP.Blender.Configuration;
using Snail.MCP.Blender.Extensions;
using Snail.MCP.Blender.Tools;
using Snail.Sdk.Mcp.Hosting.Discovery;
using Snail.Sdk.Mcp.Programs;

namespace Snail.MCP.Blender.Tests.Tools;

/// <summary>A program calls the commands itself, so a build of many steps costs one call and one reply instead of one of each per step.</summary>
/// <remarks>The program runs on Snail.Sdk's <see cref="ProgramRunner"/>, with the options and the gate this server registers; what is this
/// server's own is the catalog — the add-on's commands, the Python setting, the add-on's failures — and the limits it chose.</remarks>
public class ProgramToolsTests
{
    private readonly ScriptedBridge _bridge = new ScriptedBridge()
        .Answer(BridgeCommands.AddPrimitive, parameters => new JsonObject { ["name"] = parameters!["name"]!.DeepClone() })
        .Answer(BridgeCommands.SceneInfo, new JsonObject { ["objects"] = 3 });

    [Fact]
    public async Task Program_CallingACommandInALoop_SendsEveryCall_AndAnswersWithWhatItReturns()
    {
        var run = await RunAsync("""
            const made = [];
            for (let index = 0; index < 3; index++) {
              made.push(blender.add_primitive({ kind: "cube", name: "Crate" + index }).name);
            }
            return { made: made, objects: blender.scene_info().objects };
            """);

        Assert.Null(run.Failure);
        Assert.Equal(["Crate0", "Crate1", "Crate2"], run.Result!["made"]!.AsArray().Select(name => name!.ToString()));
        Assert.Equal(3, run.Result["objects"]!.GetValue<int>());
        Assert.Equal(4, run.Calls.Count);
        Assert.Equal(["add_primitive", "add_primitive", "add_primitive", "scene_info"], _bridge.Sent.Select(sent => sent.Command.Name));
    }

    /// <summary>A failed command is an exception the program can catch, with the add-on's own type, so one step that fails does not throw away
    /// what the rest achieved.</summary>
    [Fact]
    public async Task Program_WhoseCommandFails_CanCatchItAndCarryOn()
    {
        _bridge.Fail(BridgeCommands.DeleteObjects, RecordedAnswers.Error("delete_objects naming one that is not there"));

        var run = await RunAsync("""
            let missing = null;
            try {
              blender.delete_objects({ names: ["Ghost"] });
            } catch (failure) {
              missing = failure.type;
            }
            blender.add_primitive({ kind: "cube", name: "Crate" });
            return missing;
            """);

        Assert.Equal("NotFound", run.Result!.ToString());
        Assert.Equal(["delete_objects", "add_primitive"], _bridge.Sent.Select(sent => sent.Command.Name));
    }

    /// <summary>A failed command the program does not catch stops it with the add-on's type, so the answer says Leased or NotFound rather than
    /// only that a program stopped.</summary>
    [Fact]
    public async Task Program_WhoseCommandFailsUncaught_StopsWithTheAddOnsType()
    {
        _bridge.Fail(BridgeCommands.DeleteObjects, RecordedAnswers.Error("delete_objects naming one that is not there"));

        var run = await RunAsync("""blender.delete_objects({ names: ["Ghost"] });""");

        Assert.Equal("NotFound", run.Failure!.Type);
    }

    [Fact]
    public async Task Program_WithPythonOff_CannotReachPythonThroughIt()
    {
        var run = await RunAsync("return blender.python({ code: \"1 + 1\" });", new ServerConfig { Python = PythonAccess.Off });

        Assert.NotNull(run.Failure);
        Assert.Empty(_bridge.Sent);
    }

    /// <summary>A program is one tool call carrying whatever the script asks for, so the gate is asked about each command it sends: without that,
    /// a key issued without the Python scope ran Python by writing one line of JavaScript.</summary>
    [Fact]
    public async Task Program_CallingACommandTheKeyDoesNotOpen_IsStoppedBeforeItIsSent()
    {
        var (clients, serving) = Access.Serving("lighting", Scopes.Read, Scopes.Write, Scopes.Delete);
        using var _ = serving;

        var run = await RunAsync("return blender.python({ code: \"1 + 1\" });", clients: clients);

        Assert.Equal(ProgramFailures.NotGranted, run.Failure!.Type);
        Assert.Contains("does not open it", run.Failure.Error, StringComparison.Ordinal);
        Assert.Empty(_bridge.Sent);
    }

    /// <summary>A program is a way to spend one call, not to hide a thousand; past the cap this server sets it is stopped with what it managed.</summary>
    [Fact]
    public async Task Program_AskingForMoreCallsThanAllowed_IsStoppedAtTheCap()
    {
        var run = await RunAsync("""
            for (let index = 0; index < 250; index++) {
              blender.scene_info();
            }
            """);

        Assert.Equal(ProgramFailures.TooManyCalls, run.Failure!.Type);
        Assert.Equal(200, _bridge.Sent.Count);
    }

    [Fact]
    public async Task Program_WritingToTheLog_CarriesItBackWithTheResult()
    {
        var run = await RunAsync("""
            log("first");
            log("second");
            return input.answer;
            """, input: new JsonObject { ["answer"] = 42 });

        Assert.Equal(42, run.Result!.GetValue<int>());
        Assert.Equal(["first", "second"], run.Log);
    }

    /// <summary>The log is a global of the program alone: the commands object carries the add-on's commands and nothing else.</summary>
    /// <remarks>This server's own runner hung <c>log</c> on <c>blender</c> as well; Snail.Sdk's does not, so <c>blender.log(…)</c> is a
    /// command the catalog does not have.</remarks>
    [Fact]
    public async Task Program_CallingLogOnTheCommands_IsTold_ThereIsNoSuchCommand()
    {
        var run = await RunAsync("""blender.log("x");""");

        Assert.Equal(ProgramFailures.NoSuchCommand, run.Failure!.Type);
    }

    /// <summary>The Python setting guards the door a program knocks at too: in fallback the rule is the same one blender_python follows, so
    /// a script the tools already cover is refused whichever way it arrives.</summary>
    [Fact]
    public async Task Program_InFallback_SendingPythonTheToolsCouldDo_IsRefused_WithoutReachingBlender()
    {
        var run = await RunAsync(
            """return blender.python({ code: "import bpy\nbpy.context.scene.cycles.samples = 64\nbpy.context.scene.render.resolution_x = 1920" });""",
            new ServerConfig { Python = PythonAccess.Fallback });

        Assert.Contains("the tools already do", run.Failure!.Error, StringComparison.Ordinal);
        Assert.Empty(_bridge.Sent);
    }

    [Fact]
    public async Task Program_InFallback_SendingPythonNoToolCovers_ReachesBlender()
    {
        _bridge.Answer(BridgeCommands.Python, new JsonObject { ["stdout"] = "5.2.1" });

        var run = await RunAsync(
            """return blender.python({ code: "import bpy\nresult = bpy.app.version_string" });""",
            new ServerConfig { Python = PythonAccess.Fallback });

        Assert.Null(run.Failure);
        Assert.Equal(BridgeCommands.Python, _bridge.Sent.Single().Command);
    }

    /// <summary>Fallback is about Python, not about everything: an ordinary command a program sends goes through untouched.</summary>
    [Fact]
    public async Task Program_InFallback_SendingAnOrdinaryCommand_IsNotJudgedAsAScript()
    {
        _bridge.Answer(BridgeCommands.AddPrimitive, new JsonObject { ["name"] = "Cube" });

        var run = await RunAsync("""return blender.add_primitive({ kind: "cube" });""", new ServerConfig { Python = PythonAccess.Fallback });

        Assert.Null(run.Failure);
        Assert.Equal(BridgeCommands.AddPrimitive, _bridge.Sent.Single().Command);
    }

    /// <summary>The traffic report counts a program once, with every command it sent.</summary>
    [Fact]
    public async Task Program_IsCountedInTheTrafficReport_WithItsCommands()
    {
        var traffic = new BridgeTraffic();

        await RunAsync("blender.scene_info(); blender.scene_info();", traffic: traffic);

        Assert.Equal(1, traffic.Report()["escapeHatch"]!["programs"]!.GetValue<int>());
        Assert.Equal(2, traffic.Report()["escapeHatch"]!["programCalls"]!.GetValue<int>());
    }

    /// <summary>A program that sent no command ran all the same: counted at its catalog's first command, it went uncounted.</summary>
    [Fact]
    public async Task Program_ThatSendsNoCommand_IsCountedInTheTrafficReport()
    {
        var traffic = new BridgeTraffic();

        await RunAsync("return 1 + 1;", traffic: traffic);

        Assert.Equal(1, traffic.Report()["escapeHatch"]!["programs"]!.GetValue<int>());
        Assert.Equal(0, traffic.Report()["escapeHatch"]!["programCalls"]!.GetValue<int>());
    }

    /// <summary>A program written from <c>find_tool</c>'s answer reaches the add-on as it reads it: blender_render_job spreads its output and
    /// queue blocks over render_job, so the fields <c>find_tool</c> names at the top are the ones the command receives.</summary>
    [Fact]
    public async Task Program_WrittenFromFindToolsAnswer_SendsTheFieldsOfABlock_WhereTheCommandReadsThem()
    {
        _bridge.Answer(BridgeCommands.RenderJob, new JsonObject { ["id"] = "job-1" });
        using var server = Access.Server();
        var job = ToolCatalog.Find(server, "render four frames in the background", 8).Single(tool => tool.Name == "blender_render_job");
        var names = job.Parameters.Select(parameter => parameter.Name).ToHashSet(StringComparer.Ordinal);

        Assert.Subset(names, new HashSet<string>(["output_path", "file_format", "chunks"], StringComparer.Ordinal));

        var run = await RunAsync($$"""return blender.{{job.Command}}({ output_path: "/renders/shot_####", file_format: "OPEN_EXR", chunks: 2 });""");
        var sent = _bridge.Sent.Single();

        Assert.Null(run.Failure);
        Assert.Equal(BridgeCommands.RenderJob, sent.Command);
        Assert.Equal("OPEN_EXR", sent.Parameters!["file_format"]!.ToString());
        Assert.Equal(2, sent.Parameters["chunks"]!.GetValue<int>());
    }

    /// <summary>A command waits as long as its program has left, not the 30 seconds of a single reply: a render a program asks for is not cut
    /// off half way through a program that was given minutes.</summary>
    [Fact]
    public async Task Program_SendingACommand_GivesItTheTimeTheProgramHasLeft()
    {
        _bridge.Answer(BridgeCommands.RenderImage, new JsonObject { ["path"] = "/renders/shot.png" });

        var run = await RunAsync("""return blender.render_image({ path: "/renders/shot.png" });""", timeout: TimeSpan.FromMinutes(5));
        var waited = _bridge.Sent.Single().Timeout;

        Assert.Null(run.Failure);
        Assert.NotNull(waited);
        Assert.InRange(waited.Value, TimeSpan.FromMinutes(4), TimeSpan.FromMinutes(5));
    }

    /// <summary>Runs a program as <c>run_program</c> does: the server's options, its catalog and its gate, and the hook told once it has run.</summary>
    private async Task<ProgramRun> RunAsync(string code, ServerConfig? config = null, JsonObject? input = null, ClientSessions? clients = null, BridgeTraffic? traffic = null, TimeSpan? timeout = null)
    {
        var options = new ProgramOptions();
        TransportRegistrationExtensions.Programming(options);
        var commands = new ProgramCommands(_bridge, config ?? new ServerConfig(), Access.Advice());
        var gate = Access.Gate(clients);
        var services = new ServiceCollection().AddSingleton(traffic ?? new BridgeTraffic()).BuildServiceProvider();

        var run = await new ProgramRunner(options).RunAsync(commands, code, input, timeout ?? TimeSpan.FromSeconds(30), tool => gate.Refusal(tool, Access.Services));
        options.Ran!(run, services);

        return run;
    }
}
