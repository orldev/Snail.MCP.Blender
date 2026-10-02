using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Server;
using Snail.MCP.Blender.Application.Diagnostics;
using Snail.MCP.Blender.Application.Discovery;
using Snail.MCP.Blender.Application.Skills;
using Snail.MCP.Blender.Configuration;
using Snail.MCP.Blender.Tools.Base;

namespace Snail.MCP.Blender.Tests.Tools;

/// <summary>Finding a tool instead of writing a script: what the model is offered when it does not know the catalogue, and what a script is answered with when it writes one anyway.</summary>
public class DiscoveryToolsTests
{
    private static readonly ToolIndex Index = new(typeof(ToolIndex).Assembly);

    private readonly ScriptedBridge _bridge = new();

    [Fact]
    public async Task Find_AnIntentWhoseToolLivesInAClosedSkill_NamesIt_AndOpensTheSkillWhenAsked()
    {
        var (tools, skills) = Build();

        var answer = JsonNode.Parse((await tools.FindAsync("bevel the edges of a mesh", enable: true)).Text())!["data"]!;

        Assert.Contains("blender_mesh_bevel", answer["tools"]!.AsArray().Select(tool => tool!["tool"]!.ToString()), StringComparer.Ordinal);
        Assert.Contains("modeling", answer["enabledSkills"]!.AsArray().Select(name => name!.ToString()), StringComparer.Ordinal);
        Assert.True(skills.Status().Single(skill => skill.Name == "modeling").IsEnabled);
    }

    /// <summary>Finding a tool is enough to call it: the command and its parameters come back, so a program can send it without the skill being
    /// loaded — which is also what keeps the tool list, and the prompt built on it, unchanged.</summary>
    [Fact]
    public async Task Find_ByDefault_AnswersWithTheCommandAndItsParameters_AndLeavesTheSkillClosed()
    {
        var (tools, skills) = Build();

        var answer = JsonNode.Parse((await tools.FindAsync("bevel the edges of a mesh")).Text())!["data"]!;
        var bevel = answer["tools"]!.AsArray().Single(tool => tool!["tool"]!.ToString() == "blender_mesh_bevel")!;
        var parameters = bevel["parameters"]!.AsArray().ToDictionary(parameter => parameter!["name"]!.ToString(), parameter => parameter!);

        Assert.Equal("modeling", bevel["skill"]!.ToString());
        Assert.False(bevel["loaded"]!.GetValue<bool>());
        Assert.Equal("mesh_bevel", bevel["command"]!.ToString());
        Assert.True(parameters["name"]!["required"]!.GetValue<bool>());
        Assert.Equal("string", parameters["name"]!["type"]!.ToString());
        Assert.NotNull(parameters["width"]!["description"]);
        Assert.Empty(answer["enabledSkills"]!.AsArray());
        Assert.False(skills.Status().Single(skill => skill.Name == "modeling").IsEnabled);
    }

    /// <summary>The server's own tools — discovery, skills, transfers — send no command, and a program cannot call them; the answer says so
    /// with a null rather than inventing a name.</summary>
    [Fact]
    public void Index_AToolThatSendsNoCommand_NamesNone()
    {
        Assert.Null(Index.Named("blender_find_tool")!.Command);
        Assert.Equal("add_primitive", Index.Named("blender_add_primitive")!.Command);
    }

    [Fact]
    public async Task Find_WordsNoToolAnswers_FailsWithAHint_RatherThanGuessing()
    {
        var (tools, _) = Build();

        var result = await tools.FindAsync("zzzz qqqq");

        Assert.True(result.Failed());
        Assert.Contains("blender_run_operator", result.Text(), StringComparison.Ordinal);
    }

    [Fact]
    public void Covering_AnOperator_AnswersOnlyWhenEveryWordStandsInTheName()
    {
        Assert.Equal("blender_mesh_bevel", Index.Covering("bevel")!.Name);
        Assert.Equal("blender_insert_keyframe", Index.Covering("keyframe_insert")!.Name);
        Assert.Null(Index.Covering("frobnicate"));
    }

    /// <summary>The table promises tools by name; a rename would otherwise turn advice into a lie the analyser silently swallows.</summary>
    [Fact]
    public void EveryToolTheAdviceTableNames_IsAToolOfThisServer()
    {
        var missing = ScriptAdvice.Covers.Where(tool => Index.Named(tool) is null).ToList();

        Assert.True(missing.Count == 0, $"advice names tools that do not exist: {string.Join(", ", missing)}");
    }

    /// <summary>The script is the shape the journal recorded in a real session: settings, a material and a light, none of it through a tool.</summary>
    [Fact]
    public void Read_AScriptFromARealSession_NamesTheToolsThatDoTheSameWork()
    {
        var advice = new ScriptAdvice(Index);

        var found = advice.Read(
            """
            import bpy
            scn = bpy.context.scene
            scn.render.resolution_x = 1100
            scn.cycles.samples = 48
            scn.view_settings.look = 'AgX - Base Contrast'
            steel = bpy.data.materials.new("Steel")
            key = bpy.data.lights.new("Key", type='AREA')
            bpy.ops.mesh.bevel(offset=0.01)
            """);

        var tools = found.Select(finding => finding.Tool).ToList();

        Assert.Contains("blender_set_output", tools, StringComparer.Ordinal);
        Assert.Contains("blender_set_cycles", tools, StringComparer.Ordinal);
        Assert.Contains("blender_set_color_management", tools, StringComparer.Ordinal);
        Assert.Contains("blender_create_material", tools, StringComparer.Ordinal);
        Assert.Contains("blender_add_light", tools, StringComparer.Ordinal);
        Assert.Contains("blender_mesh_bevel", tools, StringComparer.Ordinal);
        Assert.Equal(tools.Count, tools.Distinct(StringComparer.Ordinal).Count());
    }

    /// <summary>The reading is of text, so a space on either side of a dot was enough to hide an operation from it.</summary>
    [Fact]
    public void Read_AScriptSpacedAroundItsDots_StillNamesTheTools()
    {
        var found = new ScriptAdvice(Index).Read("import bpy\nbpy.context.scene.render .resolution_x = 1920\nbpy.context.scene.cycles . samples = 64");

        Assert.Contains(found, finding => finding.Tool == "blender_set_output");
        Assert.Contains(found, finding => finding.Tool == "blender_set_cycles");
    }

    [Fact]
    public void Read_ApiNoToolCovers_AnswersNothing()
    {
        var found = new ScriptAdvice(Index).Read("import bpy\nresult = [object.name for object in bpy.data.objects if object.users == 0]");

        Assert.Empty(found);
    }

    [Fact]
    public async Task Python_InFallback_AScriptTheToolsCouldDo_IsRefused_NamingThem()
    {
        var tools = new OperatorTools(_bridge, new ServerConfig { Python = PythonAccess.Fallback }, new ScriptAdvice(Index));

        var result = await tools.PythonAsync("import bpy\nbpy.context.scene.cycles.samples = 64\nbpy.context.scene.render.resolution_x = 1920");

        Assert.True(result.Failed());
        Assert.Contains("blender_set_cycles", result.Text(), StringComparison.Ordinal);
        Assert.Contains("blender_run", result.Text(), StringComparison.Ordinal);
        Assert.Empty(_bridge.Sent);
    }

    [Fact]
    public async Task Python_InFallback_ApiNoToolCovers_StillRuns()
    {
        _bridge.Answer(BridgeCommands.Python, new JsonObject { ["stdout"] = string.Empty });
        var tools = new OperatorTools(_bridge, new ServerConfig { Python = PythonAccess.Fallback }, new ScriptAdvice(Index));

        var result = await tools.PythonAsync("import bpy\nresult = bpy.app.version_string");

        Assert.False(result.Failed());
        Assert.Single(_bridge.Sent);
    }

    [Fact]
    public async Task Python_WhenAllowed_RunsAndCarriesTheAdviceInTheReply()
    {
        _bridge.Answer(BridgeCommands.Python, new JsonObject { ["stdout"] = "done" });
        var tools = new OperatorTools(_bridge, new ServerConfig(), new ScriptAdvice(Index));

        var answer = JsonNode.Parse((await tools.PythonAsync("import bpy\nbpy.data.materials.new(\"Steel\")")).Text())!["data"]!;

        Assert.Equal("done", answer["stdout"]!.ToString());
        Assert.Equal("blender_create_material", answer["advice"]!["tools"]![0]!["tool"]!.ToString());
        Assert.Single(_bridge.Sent);
    }

    [Fact]
    public async Task Run_SendsEveryStepAsOneCommand_WithTheContinueFlag()
    {
        _bridge.Answer(BridgeCommands.Run, new JsonObject { ["ran"] = 2 });
        var tools = new SequenceTools(_bridge, new ServerConfig(), new ScriptAdvice(Index), Access.Scopes, Access.Sessions());

        var steps = new JsonArray(
            new JsonObject { ["command"] = "add_primitive", ["params"] = new JsonObject { ["kind"] = "cube" } },
            new JsonObject { ["command"] = "transform_object", ["params"] = new JsonObject { ["name"] = "Cube" } });

        await tools.RunAsync(steps, continueOnError: true);

        Assert.Equal(BridgeCommands.Run, _bridge.Sent.Single().Command);
        Assert.Equal("transform_object", _bridge.LastParameters!["commands"]![1]!["command"]!.ToString());
        Assert.True(_bridge.LastParameters["continue_on_error"]!.GetValue<bool>());
        Assert.Equal(TimeSpan.FromSeconds(120), _bridge.Sent.Single().Timeout);
    }

    /// <summary>A script inside a sequence is the same script: fallback reads it the way it reads blender_python, or a sequence would be the way
    /// around the setting.</summary>
    [Fact]
    public async Task Run_InFallback_AStepScriptTheToolsCouldDo_IsRefused_NamingTheStepAndTheTools()
    {
        var tools = new SequenceTools(_bridge, new ServerConfig { Python = PythonAccess.Fallback }, new ScriptAdvice(Index), Access.Scopes, Access.Sessions());
        var steps = new JsonArray(
            new JsonObject { ["command"] = "add_primitive", ["params"] = new JsonObject { ["kind"] = "cube" } },
            new JsonObject { ["command"] = "python", ["params"] = new JsonObject { ["code"] = "import bpy\nbpy.context.scene.cycles.samples = 64\nbpy.context.scene.render.resolution_x = 1920" } });

        var result = await tools.RunAsync(steps);

        Assert.True(result.Failed());
        Assert.Contains("Step 2", result.Text(), StringComparison.Ordinal);
        Assert.Contains("blender_set_cycles", result.Text(), StringComparison.Ordinal);
        Assert.Empty(_bridge.Sent);
    }

    [Fact]
    public async Task Run_InFallback_AStepScriptNoToolCovers_IsSent()
    {
        _bridge.Answer(BridgeCommands.Run, new JsonObject { ["ran"] = 1 });
        var tools = new SequenceTools(_bridge, new ServerConfig { Python = PythonAccess.Fallback }, new ScriptAdvice(Index), Access.Scopes, Access.Sessions());

        var result = await tools.RunAsync(new JsonArray(new JsonObject { ["command"] = "python", ["params"] = new JsonObject { ["code"] = "import bpy\nresult = bpy.app.version_string" } }));

        Assert.False(result.Failed());
        Assert.Single(_bridge.Sent);
    }

    [Fact]
    public async Task Run_WithAPythonStep_IsRefusedWhenPythonIsOff()
    {
        var tools = new SequenceTools(_bridge, new ServerConfig { Python = PythonAccess.Off }, new ScriptAdvice(Index), Access.Scopes, Access.Sessions());

        var result = await tools.RunAsync(new JsonArray(new JsonObject { ["command"] = "python", ["params"] = new JsonObject { ["code"] = "1" } }));

        Assert.True(result.Failed());
        Assert.Empty(_bridge.Sent);
    }

    /// <summary>The share the whole change is judged by: it has to be readable without digging through the journal.</summary>
    [Fact]
    public void Traffic_CountsWhatWentThroughPython_AgainstTheRest()
    {
        var traffic = new BridgeTraffic();
        traffic.Record(BridgeCommands.Python, TimeSpan.FromMilliseconds(10), null);
        traffic.Record(BridgeCommands.Run, TimeSpan.FromMilliseconds(10), null);
        traffic.Record(BridgeCommands.AddPrimitive, TimeSpan.FromMilliseconds(10), null);
        traffic.Record(BridgeCommands.AddPrimitive, TimeSpan.FromMilliseconds(10), null);

        var report = traffic.Report()["escapeHatch"]!;

        Assert.Equal(1, report["python"]!.GetValue<int>());
        Assert.Equal(1, report["sequences"]!.GetValue<int>());
        Assert.Equal(0.25, report["pythonShare"]!.GetValue<double>());
    }

    private static (DiscoveryTools Tools, SkillCatalog Skills) Build()
    {
        var collection = new McpServerPrimitiveCollection<McpServerTool>();
        var services = new ServiceCollection().BuildServiceProvider();
        var skills = new SkillCatalog(SkillCatalog.Discover(typeof(ToolIndex).Assembly), collection, services, TimeProvider.System);

        return (new DiscoveryTools(Index, skills), skills);
    }
}
