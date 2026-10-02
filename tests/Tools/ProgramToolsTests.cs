using Snail.MCP.Blender.Adapters.Programs;
using Snail.MCP.Blender.Application.Diagnostics;
using Snail.MCP.Blender.Application.Discovery;
using Snail.MCP.Blender.Configuration;
using Snail.MCP.Blender.Tools.Base;

namespace Snail.MCP.Blender.Tests.Tools;

/// <summary>A program calls the commands itself, so a build of many steps costs one call and one reply instead of one of each per step.</summary>
public class ProgramToolsTests
{
    private readonly ScriptedBridge _bridge = new ScriptedBridge()
        .Answer(BridgeCommands.AddPrimitive, parameters => new JsonObject { ["name"] = parameters!["name"]!.DeepClone() })
        .Answer(BridgeCommands.SceneInfo, new JsonObject { ["objects"] = 3 });

    [Fact]
    public async Task Program_CallingACommandInALoop_SendsEveryCall_AndAnswersWithWhatItReturns()
    {
        var result = await RunAsync("""
            const made = [];
            for (let index = 0; index < 3; index++) {
              made.push(blender.add_primitive({ kind: "cube", name: "Crate" + index }).name);
            }
            return { made: made, objects: blender.scene_info().objects };
            """);

        Assert.Equal(["Crate0", "Crate1", "Crate2"], result["data"]!["result"]!["made"]!.AsArray().Select(name => name!.ToString()));
        Assert.Equal(3, result["data"]!["result"]!["objects"]!.GetValue<int>());
        Assert.Equal(4, result["data"]!["calls"]!.GetValue<int>());
        Assert.Equal(["add_primitive", "add_primitive", "add_primitive", "scene_info"], _bridge.Sent.Select(sent => sent.Command.Name));
    }

    /// <summary>A failed command is an exception the program can catch, so one step that fails does not throw away what the rest achieved.</summary>
    [Fact]
    public async Task Program_WhoseCommandFails_CanCatchItAndCarryOn()
    {
        _bridge.Fail(BridgeCommands.DeleteObjects, RecordedAnswers.Error("delete_objects naming one that is not there"));

        var result = await RunAsync("""
            let missing = null;
            try {
              blender.delete_objects({ names: ["Ghost"] });
            } catch (failure) {
              missing = failure.type;
            }
            blender.add_primitive({ kind: "cube", name: "Crate" });
            return missing;
            """);

        Assert.Equal("NotFound", result["data"]!["result"]!.ToString());
        Assert.Equal(["delete_objects", "add_primitive"], _bridge.Sent.Select(sent => sent.Command.Name));
    }

    [Fact]
    public async Task Program_WithPythonOff_CannotReachPythonThroughIt()
    {
        var refused = await RunAsync("return blender.python({ code: \"1 + 1\" });", new ServerConfig { Python = PythonAccess.Off });

        Assert.False(refused["ok"]!.GetValue<bool>());
        Assert.Empty(_bridge.Sent);
    }

    /// <summary>A program is one tool call carrying whatever the script asks for, so the key is asked about each command it sends: without that,
    /// a key issued without the Python scope ran Python by writing one line of JavaScript.</summary>
    [Fact]
    public async Task Program_CallingACommandTheKeyDoesNotOpen_IsStoppedBeforeItIsSent()
    {
        var (clients, serving) = Access.Serving("lighting", Scopes.Read, Scopes.Write, Scopes.Delete);
        using var _ = serving;
        var settings = new ServerConfig { Python = PythonAccess.Allow };
        var tools = new ProgramTools(
            new JavaScriptPrograms(_bridge, settings, new ScriptAdvice(new ToolIndex(typeof(ProgramTools).Assembly)), Access.Scopes, clients),
            new BridgeTraffic());

        var stopped = JsonNode.Parse((await tools.ProgramAsync("return blender.python({ code: \"1 + 1\" });", null, 30)).Text())!;

        Assert.False(stopped["ok"]!.GetValue<bool>());
        Assert.Contains("does not open it", stopped["error"]!.ToString(), StringComparison.Ordinal);
        Assert.Empty(_bridge.Sent);
    }

    [Fact]
    public async Task Program_ThatNeverEnds_IsStoppedByItsTimeout()
    {
        var stopped = await RunAsync("while (true) { }", timeoutSeconds: 1);

        Assert.False(stopped["ok"]!.GetValue<bool>());
        Assert.Contains("longer", stopped["error"]!.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>A program is a way to spend one call, not to hide a thousand; past the cap it is stopped with what it managed so far.</summary>
    [Fact]
    public async Task Program_AskingForMoreCallsThanAllowed_IsStoppedAtTheCap()
    {
        var stopped = await RunAsync($$"""
            for (let index = 0; index < {{JavaScriptPrograms.MostCalls + 50}}; index++) {
              blender.add_primitive({ kind: "cube", name: "Crate" + index });
            }
            return "done";
            """);

        Assert.False(stopped["ok"]!.GetValue<bool>());
        Assert.Equal(JavaScriptPrograms.MostCalls, _bridge.Sent.Count);
    }

    [Fact]
    public async Task Program_WritingToTheLog_CarriesItBackWithTheResult()
    {
        var result = await RunAsync("""
            log("first");
            log("second");
            return input.answer;
            """, input: new JsonObject { ["answer"] = 42 });

        Assert.Equal(42, result["data"]!["result"]!.GetValue<int>());
        Assert.Equal(["first", "second"], result["data"]!["log"]!.AsArray().Select(line => line!.ToString()));
    }

    private async Task<JsonNode> RunAsync(string code, ServerConfig? config = null, JsonObject? input = null, int timeoutSeconds = 30)
    {
        var settings = config ?? new ServerConfig();
        var tools = new ProgramTools(new JavaScriptPrograms(_bridge, settings, new ScriptAdvice(new ToolIndex(typeof(ProgramTools).Assembly)), Access.Scopes, Access.Sessions()), new BridgeTraffic());

        return JsonNode.Parse((await tools.ProgramAsync(code, input, timeoutSeconds)).Text())!;
    }
}
