using System.Reflection;
using System.Text.Json;
using ModelContextProtocol.Server;
using Snail.MCP.Blender.Application.Diagnostics;
using Snail.MCP.Blender.Application.Discovery;
using Snail.MCP.Blender.Configuration;
using Snail.MCP.Blender.Tools;
using Snail.MCP.Blender.Tools.Base;
using Snail.Sdk.Mcp.Hosting.Discovery;

namespace Snail.MCP.Blender.Tests.Tools;

/// <summary>Finding a tool instead of writing a script: what find_tool ranks for the server's own words, and what a script is answered with when
/// it writes one anyway.</summary>
public class DiscoveryToolsTests
{
    private readonly ScriptedBridge _bridge = new();

    /// <summary>A tool that lives in a skill is found all the same, with the skill that loads it.</summary>
    [Fact]
    public void Find_AnIntentWhoseToolLivesInASkill_NamesIt_WithItsSkill()
    {
        var found = ToolCatalog.Find(Access.Catalog, "bevel the edges of a mesh", 8, Access.Discovery());
        var bevel = found.Single(tool => tool.Name == "blender_mesh_bevel");

        Assert.Equal("modeling", bevel.Skill);
    }

    /// <summary>Finding a tool is enough to call it: its parameters come back, and the command a program sends for it, so a program can send it
    /// without the skill being loaded.</summary>
    [Fact]
    public void Find_AnswersWithTheParameters_AndTheCommandAProgramSends()
    {
        var bevel = ToolCatalog.Find(Access.Catalog, "bevel the edges of a mesh", 8, Access.Discovery()).Single(tool => tool.Name == "blender_mesh_bevel");
        var parameters = bevel.Parameters.ToDictionary(parameter => parameter.Name);

        Assert.Equal("mesh_bevel", Access.Discovery().CommandOf!(bevel.Name));
        Assert.True(parameters["name"].IsRequired);
        Assert.Equal("string", parameters["name"].Type);
        Assert.NotNull(parameters["width"].Description);
    }

    /// <summary>The server's own tools — discovery, skills, transfers — send no command, and a program cannot call them; the catalog says so
    /// with a null rather than inventing a name.</summary>
    [Fact]
    public void Command_OfAToolThatSendsNone_IsNone()
    {
        Assert.Null(BridgeCommands.CommandOf("find_tool"));
        Assert.Null(BridgeCommands.CommandOf("blender_upload"));
        Assert.Equal("add_primitive", BridgeCommands.CommandOf("blender_add_primitive"));
    }

    /// <summary>Words no tool answers find nothing: a form needs four letters on both sides, so the "z" of "+z" in blender_mesh_extrude's
    /// description no longer answers "zzzz", nor the "e" and "g" of "e.g." every word that starts with them.</summary>
    [Fact]
    public void Find_WordsNoToolAnswers_FindsNothing_RatherThanGuessing()
    {
        Assert.Empty(ToolCatalog.Find(Access.Catalog, "zzzz qqqq", 8, Access.Discovery()));
    }

    /// <summary>A program written from <c>find_tool</c>'s answer sends the parameters as the add-on reads them: snake_case, and only those the
    /// command takes, since the add-on ignores a name it does not read.</summary>
    [Fact]
    public void Parameter_OfACommand_IsNamedAsTheAddOnReadsIt()
    {
        var parameterOf = Access.Discovery().ParameterOf!;

        Assert.Equal("size_y", parameterOf("blender_add_light", "sizeY"));
        Assert.Equal("ies_path", parameterOf("blender_add_light", "iesPath"));
        Assert.Equal("type", parameterOf("blender_add_light", "type"));
        Assert.Null(parameterOf("blender_journal", "agent"));
        Assert.Equal("since", parameterOf("blender_journal", "since"));
    }

    /// <summary>A field of a block is asked by its path: a block the command takes keeps its fields inside it in snake_case, and a block the
    /// command does not take is one the tool spreads over the command, so its fields answer by their top-level names and the block by none.</summary>
    [Fact]
    public void Parameter_AFieldOfABlock_IsPlacedWhereTheCommandReadsIt()
    {
        var parameterOf = Access.Discovery().ParameterOf!;

        Assert.Equal("focus.distance", parameterOf("blender_set_camera_optics", "focus.distance"));
        Assert.Equal("pack_for_farm.pack_external", parameterOf("blender_render_budget", "packForFarm.packExternal"));
        Assert.Equal("apply.simplify.child_particles", parameterOf("blender_render_budget", "apply.simplify.childParticles"));
        Assert.Null(parameterOf("blender_render_job", "output"));
        Assert.Equal("file_format", parameterOf("blender_render_job", "output.fileFormat"));
        Assert.Equal("max_parallel", parameterOf("blender_render_job", "queue.maxParallel"));
    }

    /// <summary>blender_render_job takes what it writes and how it runs as two blocks and spreads their fields over the render_job command, so
    /// <c>find_tool</c> names those fields at the top, where a program sends them, and not the blocks, which the add-on would never read.</summary>
    [Fact]
    public void Find_RenderJob_NamesTheFieldsOfItsBlocks_AtTheTopOfTheCommand()
    {
        using var server = Access.Server();

        var job = ToolCatalog.Find(server, "render four frames in the background", 8).Single(tool => tool.Name == "blender_render_job");
        var names = job.Parameters.Select(parameter => parameter.Name).ToList();

        Assert.Equal("render_job", job.Command);
        Assert.Contains("file_format", names, StringComparer.Ordinal);
        Assert.Contains("samples", names, StringComparer.Ordinal);
        Assert.Contains("chunks", names, StringComparer.Ordinal);
        Assert.Contains("retries", names, StringComparer.Ordinal);
        Assert.DoesNotContain("output", names, StringComparer.Ordinal);
        Assert.DoesNotContain("queue", names, StringComparer.Ordinal);
    }

    /// <summary>A block the add-on reads whole comes with its fields, in the names the add-on reads inside it, instead of a bare object a
    /// program could only guess at.</summary>
    [Fact]
    public void Find_CameraOptics_NamesTheFieldsOfFocus_AsTheAddOnReadsThem()
    {
        using var server = Access.Server();

        var optics = ToolCatalog.Find(server, "focus distance and aperture of the camera", 8).Single(tool => tool.Name == "blender_set_camera_optics");
        var focus = optics.Parameters.Single(parameter => parameter.Name == "focus");

        Assert.Equal("object", focus.Type);
        Assert.Equal(["distance", "object", "bone"], focus.Fields!.Select(field => field.Name));
    }

    /// <summary>Every block a tool sends whole is answered with the fields <see cref="Block.ToWire"/> writes, and every block a tool spreads
    /// over its command with those fields among the command's own parameters: a field renamed on one side only would hand a program a name
    /// the add-on ignores without a word.</summary>
    [Fact]
    public void Find_EveryBlockParameter_IsAnsweredWithTheFieldsTheAddOnReceives()
    {
        using var server = Access.Server();
        var catalog = ToolCatalog.Of(server).ToDictionary(tool => tool.Name, StringComparer.Ordinal);
        var blocks = BlockParameters();
        var wrong = new List<string>();

        foreach (var (tool, parameter, block) in blocks)
        {
            var parameters = catalog[tool].Parameters;
            var sent = Paths(Filled(block).ToWire().AsObject());
            var whole = parameters.SingleOrDefault(each => each.Name == JsonNamingPolicy.SnakeCaseLower.ConvertName(parameter));
            var answered = whole is null ? parameters.Select(each => each.Name).Where(sent.Contains).ToHashSet(StringComparer.Ordinal) : Paths(whole.Fields ?? []);

            if (!answered.SetEquals(sent))
            {
                wrong.Add($"{tool} {parameter}: sends {string.Join(", ", sent.Order(StringComparer.Ordinal))}; answered {string.Join(", ", answered.Order(StringComparer.Ordinal))}");
            }
        }

        Assert.True(blocks.Count >= 38, $"only {blocks.Count} block parameters were found");
        Assert.True(wrong.Count == 0, string.Join(Environment.NewLine, wrong));
    }

    [Fact]
    public void Covering_AnOperator_AnswersOnlyWhenEveryWordStandsInTheName()
    {
        var advice = Access.Advice();

        Assert.Equal("blender_mesh_bevel", advice.Covering("bevel")!.Name);
        Assert.Equal("blender_insert_keyframe", advice.Covering("keyframe_insert")!.Name);
        Assert.Null(advice.Covering("frobnicate"));
    }

    /// <summary>The table promises tools by name; a rename would otherwise turn advice into a lie the analyser silently swallows.</summary>
    [Fact]
    public void EveryToolTheAdviceTableNames_IsAToolOfThisServer()
    {
        var names = Access.Catalog.Select(tool => tool.Name).ToHashSet(StringComparer.Ordinal);
        var missing = ScriptAdvice.Covers.Where(tool => !names.Contains(tool)).ToList();

        Assert.True(missing.Count == 0, $"advice names tools that do not exist: {string.Join(", ", missing)}");
    }

    /// <summary>The script is the shape the journal recorded in a real session: settings, a material and a light, none of it through a tool.</summary>
    [Fact]
    public void Read_AScriptFromARealSession_NamesTheToolsThatDoTheSameWork()
    {
        var advice = Access.Advice();

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
        var found = Access.Advice().Read("import bpy\nbpy.context.scene.render .resolution_x = 1920\nbpy.context.scene.cycles . samples = 64");

        Assert.Contains(found, finding => finding.Tool == "blender_set_output");
        Assert.Contains(found, finding => finding.Tool == "blender_set_cycles");
    }

    [Fact]
    public void Read_ApiNoToolCovers_AnswersNothing()
    {
        var found = Access.Advice().Read("import bpy\nresult = [object.name for object in bpy.data.objects if object.users == 0]");

        Assert.Empty(found);
    }

    [Fact]
    public async Task Python_InFallback_AScriptTheToolsCouldDo_IsRefused_NamingThem()
    {
        var tools = new OperatorTools(_bridge, new ServerConfig { Python = PythonAccess.Fallback }, Access.Advice());

        var result = await tools.PythonAsync("import bpy\nbpy.context.scene.cycles.samples = 64\nbpy.context.scene.render.resolution_x = 1920");

        Assert.False(result.IsOk());
        Assert.Contains("blender_set_cycles", result.Text(), StringComparison.Ordinal);
        Assert.Contains("run_tools", result.Text(), StringComparison.Ordinal);
        Assert.Empty(_bridge.Sent);
    }

    [Fact]
    public async Task Python_InFallback_ApiNoToolCovers_StillRuns()
    {
        _bridge.Answer(BridgeCommands.Python, new JsonObject { ["stdout"] = string.Empty });
        var tools = new OperatorTools(_bridge, new ServerConfig { Python = PythonAccess.Fallback }, Access.Advice());

        var result = await tools.PythonAsync("import bpy\nresult = bpy.app.version_string");

        Assert.True(result.IsOk());
        Assert.Single(_bridge.Sent);
    }

    [Fact]
    public async Task Python_WhenAllowed_RunsAndCarriesTheAdviceInTheReply()
    {
        _bridge.Answer(BridgeCommands.Python, new JsonObject { ["stdout"] = "done" });
        var tools = new OperatorTools(_bridge, new ServerConfig(), Access.Advice());

        var answer = ((await tools.PythonAsync("import bpy\nbpy.data.materials.new(\"Steel\")"))).Data()!;

        Assert.Equal("done", answer["stdout"]!.ToString());
        Assert.Equal("blender_create_material", answer["advice"]!["tools"]![0]!["tool"]!.ToString());
        Assert.Single(_bridge.Sent);
    }

    [Fact]
    public async Task Run_SendsEveryStepAsOneCommand_WithTheContinueFlag()
    {
        _bridge.Answer(BridgeCommands.Run, new JsonObject { ["ran"] = 2 });
        var tools = new SequenceTools(_bridge, new ServerConfig(), Access.Advice(), Access.Served());

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
        var tools = new SequenceTools(_bridge, new ServerConfig { Python = PythonAccess.Fallback }, Access.Advice(), Access.Served());
        var steps = new JsonArray(
            new JsonObject { ["command"] = "add_primitive", ["params"] = new JsonObject { ["kind"] = "cube" } },
            new JsonObject { ["command"] = "python", ["params"] = new JsonObject { ["code"] = "import bpy\nbpy.context.scene.cycles.samples = 64\nbpy.context.scene.render.resolution_x = 1920" } });

        var result = await tools.RunAsync(steps);

        Assert.False(result.IsOk());
        Assert.Contains("Step 2", result.Text(), StringComparison.Ordinal);
        Assert.Contains("blender_set_cycles", result.Text(), StringComparison.Ordinal);
        Assert.Empty(_bridge.Sent);
    }

    [Fact]
    public async Task Run_InFallback_AStepScriptNoToolCovers_IsSent()
    {
        _bridge.Answer(BridgeCommands.Run, new JsonObject { ["ran"] = 1 });
        var tools = new SequenceTools(_bridge, new ServerConfig { Python = PythonAccess.Fallback }, Access.Advice(), Access.Served());

        var result = await tools.RunAsync(new JsonArray(new JsonObject { ["command"] = "python", ["params"] = new JsonObject { ["code"] = "import bpy\nresult = bpy.app.version_string" } }));

        Assert.True(result.IsOk());
        Assert.Single(_bridge.Sent);
    }

    [Fact]
    public async Task Run_WithAPythonStep_IsRefusedWhenPythonIsOff()
    {
        var tools = new SequenceTools(_bridge, new ServerConfig { Python = PythonAccess.Off }, Access.Advice(), Access.Served());

        var result = await tools.RunAsync(new JsonArray(new JsonObject { ["command"] = "python", ["params"] = new JsonObject { ["code"] = "1" } }));

        Assert.False(result.IsOk());
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

    /// <summary>Every parameter of a tool that takes a block, or a list of blocks, with the block's type.</summary>
    private static List<(string Tool, string Parameter, Type Block)> BlockParameters()
    {
        var tools = typeof(Block).Assembly.GetTypes()
            .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            .Select(method => (Method: method, Name: method.GetCustomAttribute<McpServerToolAttribute>()?.Name));
        var parameters = tools
            .Where(tool => tool.Name is not null)
            .SelectMany(tool => tool.Method.GetParameters().Select(parameter => (Tool: tool.Name!, Parameter: parameter.Name!, Block: BlockOf(parameter.ParameterType))));

        return [.. parameters.Where(each => each.Block is not null).Select(each => (each.Tool, each.Parameter, each.Block!))];
    }

    private static Type? BlockOf(Type type)
    {
        var element = type.IsArray ? type.GetElementType()! : type;

        return element.IsSubclassOf(typeof(Block)) ? element : null;
    }

    /// <summary>A block with every field set, the blocks inside it filled too, so its wire form carries every name it can carry.</summary>
    private static Block Filled(Type block)
    {
        var filled = (Block)Activator.CreateInstance(block, nonPublic: true)!;

        foreach (var property in block.GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(property => property.CanWrite))
        {
            property.SetValue(filled, Sample(Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType));
        }

        return filled;
    }

    private static object? Sample(Type type) =>
        type == typeof(string) ? "x"
        : type == typeof(int) ? 1
        : type == typeof(double) ? 1.0
        : type == typeof(bool) ? true
        : type.IsEnum ? Enum.GetValues(type).GetValue(0)
        : type.IsArray ? Array.CreateInstance(type.GetElementType()!, 0)
        : type.IsSubclassOf(typeof(Block)) ? Filled(type)
        : null;

    /// <summary>The dotted paths of every name a block's wire form carries, the blocks inside it included.</summary>
    private static HashSet<string> Paths(JsonObject wire, string prefix = "")
    {
        HashSet<string> paths = new(StringComparer.Ordinal);

        foreach (var (name, value) in wire)
        {
            paths.Add(prefix + name);

            if (value is JsonObject inner)
            {
                paths.UnionWith(Paths(inner, $"{prefix}{name}."));
            }
        }

        return paths;
    }

    /// <summary>The dotted paths of every field <c>find_tool</c> answers for a block, the fields of a block inside it included and those of a
    /// list's items left to the items.</summary>
    private static HashSet<string> Paths(IReadOnlyList<ToolParameter> fields, string prefix = "")
    {
        HashSet<string> paths = new(StringComparer.Ordinal);

        foreach (var field in fields)
        {
            paths.Add(prefix + field.Name);

            if (field.Fields is { } inner && !field.Type.EndsWith("[]", StringComparison.Ordinal))
            {
                paths.UnionWith(Paths(inner, $"{prefix}{field.Name}."));
            }
        }

        return paths;
    }
}
