using Snail.MCP.Blender.Application.Discovery;
using Snail.MCP.Blender.Configuration;
using Snail.MCP.Blender.Tools;
using Snail.MCP.Blender.Tools.Base;

namespace Snail.MCP.Blender.Tests.Tools;

/// <summary>Each base tool sends the right command with the right wire names, and leaves out what the caller did not pass so the add-on's defaults apply.</summary>
public class BaseToolsTests
{
    private readonly ScriptedBridge _bridge = new ScriptedBridge().Answer(BridgeCommands.AddPrimitive, new JsonObject { ["name"] = "Cube" });

    private static readonly ScriptAdvice Advice = new(new ToolIndex(typeof(ToolIndex).Assembly));

    [Fact]
    public async Task AddPrimitive_OnlyKindAndLocation_SendsThoseAndNothingElse()
    {
        var tools = new ObjectTools(_bridge);

        var response = JsonNode.Parse((await tools.AddPrimitiveAsync("cube", location: [1, 2, 3])).Text())!;

        var parameters = _bridge.LastParameters!;

        Assert.Equal(BridgeCommands.AddPrimitive, _bridge.Sent.Single().Command);
        Assert.Equal(["kind", "location"], parameters.Select(pair => pair.Key));
        Assert.Equal(3, parameters["location"]![2]!.GetValue<double>());
        Assert.Equal("Cube", response["data"]!["name"]!.ToString());
    }

    [Fact]
    public async Task UpdateObject_ClearParent_SendsAnExplicitNullParent()
    {
        var tools = new ObjectTools(_bridge);

        (await tools.UpdateObjectAsync("Cube", clearParent: true)).Text();

        var parameters = _bridge.LastParameters!;

        Assert.True(parameters.ContainsKey("parent"));
        Assert.Null(parameters["parent"]);
        Assert.True(parameters["keep_transform"]!.GetValue<bool>());
    }

    [Fact]
    public async Task UpdateObject_NoParentArguments_LeavesParentOut()
    {
        var tools = new ObjectTools(_bridge);

        (await tools.UpdateObjectAsync("Cube", newName: "Box")).Text();

        Assert.False(_bridge.LastParameters!.ContainsKey("parent"));
        Assert.Equal("Box", _bridge.LastParameters["new_name"]!.ToString());
    }

    [Fact]
    public async Task SetCamera_LensAndAim_SendWireNames_WithoutDepthOfField()
    {
        var tools = new CameraTools(_bridge);

        (await tools.SetCameraAsync("Camera", lens: 35, lookAt: [0, 0, 1])).Text();

        var parameters = _bridge.LastParameters!;

        Assert.Equal(BridgeCommands.SetCamera, _bridge.Sent.Single().Command);
        Assert.Equal(35, parameters["lens"]!.GetValue<double>());
        Assert.Equal(1, parameters["look_at"]![2]!.GetValue<double>());
        Assert.False(parameters.ContainsKey("dof"));
    }

    [Fact]
    public async Task SetCamera_NoDepthOfFieldValues_SendsNoDof()
    {
        var tools = new CameraTools(_bridge);

        (await tools.SetCameraAsync("Camera", lens: 85)).Text();

        Assert.False(_bridge.LastParameters!.ContainsKey("dof"));
    }

    /// <summary>A timeout outside the range used to be clamped in silence, so a model asking for 5000 seconds got 600 without knowing why its render was cut short.</summary>
    [Fact]
    public async Task RunOperator_TimeoutAboveTheLimit_IsRefusedBeforeReachingBlender()
    {
        var tools = new OperatorTools(_bridge, new ServerConfig(), Advice);

        var result = await tools.RunOperatorAsync("mesh.primitive_cube_add", new JsonObject { ["size"] = 2 }, timeoutSeconds: 5000);

        Assert.True(result.Failed());
        Assert.Contains(ToolLimits.MaxTimeoutSeconds.ToString(), result.Text(), StringComparison.Ordinal);
        Assert.Empty(_bridge.Sent);
    }

    [Fact]
    public async Task FileTools_OpenFile_WaitsLongerThanTheDefault()
    {
        var tools = new FileTools(_bridge);

        (await tools.OpenFileAsync("/tmp/scene.blend")).Text();

        Assert.Equal(TimeSpan.FromSeconds(120), _bridge.Sent.Single().Timeout);
        Assert.Equal("/tmp/scene.blend", _bridge.LastParameters!["path"]!.ToString());
    }

    [Fact]
    public async Task FileTools_OpenProject_SendsTheNameAndHowToStart_AndWaitsLikeAFileOpens()
    {
        var tools = new FileTools(_bridge);

        (await tools.OpenProjectAsync("robot", from: "current", discard: true)).Text();

        Assert.Equal(BridgeCommands.OpenProject, _bridge.Sent.Single().Command);
        Assert.Equal(TimeSpan.FromSeconds(120), _bridge.Sent.Single().Timeout);
        Assert.Equal("robot", _bridge.LastParameters!["name"]!.ToString());
        Assert.Equal("current", _bridge.LastParameters["from"]!.ToString());
        Assert.True(_bridge.LastParameters["discard"]!.GetValue<bool>());
    }

    [Fact]
    public async Task Light_AddAndSet_ShareTheSameSettingNames()
    {
        var tools = new LightTools(_bridge);

        (await tools.AddLightAsync("SPOT", spotSize: 45, energy: 1000)).Text();
        (await tools.SetLightAsync("Spot", spotSize: 30)).Text();

        Assert.Equal(45, _bridge.Sent[0].Parameters!["spot_size"]!.GetValue<double>());
        Assert.Equal(1000, _bridge.Sent[0].Parameters!["energy"]!.GetValue<double>());
        Assert.Equal(30, _bridge.Sent[1].Parameters!["spot_size"]!.GetValue<double>());
    }

    [Fact]
    public async Task Python_WhenConfigurationForbidsIt_IsRefusedBeforeReachingBlender()
    {
        var tools = new OperatorTools(_bridge, new ServerConfig { Python = PythonAccess.Off }, Advice);

        var response = JsonNode.Parse((await tools.PythonAsync("import bpy")).Text())!;

        Assert.False(response["ok"]!.GetValue<bool>());
        Assert.Empty(_bridge.Sent);
    }

    [Fact]
    public async Task Snapshot_LeavesTheDirectoryToTheAddOn_WhichMayLiveOnAnotherMachine()
    {
        var tools = new FileTools(_bridge);

        (await tools.SnapshotAsync("save", "before", "two scenes")).Text();

        Assert.Equal(BridgeCommands.Snapshot, _bridge.Sent.Single().Command);
        Assert.False(_bridge.LastParameters!.ContainsKey("directory"));
        Assert.Equal("two scenes", _bridge.LastParameters["note"]!.ToString());
    }

    [Fact]
    public async Task AddLight_KelvinAndShape_SendWireNames()
    {
        var tools = new LightTools(_bridge);

        (await tools.AddLightAsync("AREA", temperature: 3200, shape: "RECTANGLE", sizeY: 0.5, spread: 90, iesPath: "/ies/spot.ies")).Text();

        var parameters = _bridge.LastParameters!;

        Assert.Equal(3200, parameters["temperature"]!.GetValue<double>());
        Assert.Equal("RECTANGLE", parameters["shape"]!.ToString());
        Assert.Equal(0.5, parameters["size_y"]!.GetValue<double>());
        Assert.Equal("/ies/spot.ies", parameters["ies_path"]!.ToString());
    }

    [Fact]
    public async Task Batch_LeavesTheDirectoryToTheAddOn_AndLeaseDefaultsToList()
    {
        var tools = new TeamTools(_bridge, new ServerConfig { DataDirectory = "/data/snail" }, Advice, Access.Scopes, Access.Sessions());

        (await tools.BatchAsync(new JsonArray(new JsonObject { ["command"] = "add_primitive", ["params"] = new JsonObject { ["kind"] = "cube" } }), output: "/out/crate.blend")).Text();
        (await tools.LeaseAsync()).Text();

        Assert.Equal(BridgeCommands.BatchJob, _bridge.Sent[0].Command);
        Assert.False(_bridge.Sent[0].Parameters!.ContainsKey("directory"));
        Assert.Equal("add_primitive", _bridge.Sent[0].Parameters!["commands"]![0]!["command"]!.ToString());
        Assert.Equal("list", _bridge.Sent[1].Parameters!["action"]!.ToString());
    }

    [Fact]
    public async Task RunOperator_ScriptOperators_AreRefusedWhenPythonIsOff()
    {
        var tools = new OperatorTools(_bridge, new ServerConfig { Python = PythonAccess.Off }, Advice);

        var refused = JsonNode.Parse((await tools.RunOperatorAsync("script.python_file_run", new JsonObject { ["filepath"] = "/tmp/x.py" })).Text())!;
        (await tools.RunOperatorAsync("mesh.primitive_cube_add")).Text();

        Assert.False(refused["ok"]!.GetValue<bool>());
        Assert.Single(_bridge.Sent);
        Assert.True(ScriptAccess.RunsScripts("text.run_script"));
        Assert.False(ScriptAccess.RunsScripts("object.shade_smooth"));
    }

    /// <summary>Installing and enabling an add-on runs its Python, and so does opening a file with its scripts trusted; with the setting off, the
    /// pair addon_install and addon_enable was a way around it.</summary>
    [Fact]
    public async Task RunOperator_OperatorsThatInstallOrTrustCode_AreRefusedWhenPythonIsOff()
    {
        var tools = new OperatorTools(_bridge, new ServerConfig { Python = PythonAccess.Off }, Advice);

        var install = await tools.RunOperatorAsync("preferences.addon_install", new JsonObject { ["filepath"] = "/tmp/x.zip" });
        var extension = await tools.RunOperatorAsync("extensions.package_install_files", new JsonObject { ["filepath"] = "/tmp/x.zip" });
        var trusted = await tools.RunOperatorAsync("wm.open_mainfile", new JsonObject { ["filepath"] = "/tmp/x.blend", ["use_scripts"] = true });
        await tools.RunOperatorAsync("wm.open_mainfile", new JsonObject { ["filepath"] = "/tmp/x.blend" });

        Assert.True(install.Failed());
        Assert.True(extension.Failed());
        Assert.True(trusted.Failed());
        Assert.Single(_bridge.Sent);
        Assert.True(ScriptAccess.ContainsPython(new JsonArray(new JsonObject { ["command"] = "run_operator", ["params"] = new JsonObject { ["name"] = "preferences.addon_enable", ["params"] = new JsonObject { ["module"] = "x" } } })));
    }

    [Fact]
    public async Task Batch_InFallback_AStepScriptTheToolsCouldDo_IsRefused()
    {
        var tools = new TeamTools(_bridge, new ServerConfig { Python = PythonAccess.Fallback }, Advice, Access.Scopes, Access.Sessions());

        var result = await tools.BatchAsync(new JsonArray(new JsonObject { ["command"] = "python", ["params"] = new JsonObject { ["code"] = "import bpy\nbpy.context.scene.cycles.samples = 64\nbpy.context.scene.render.resolution_x = 1920" } }));

        Assert.True(result.Failed());
        Assert.Contains("blender_set_cycles", result.Text(), StringComparison.Ordinal);
        Assert.Empty(_bridge.Sent);
    }

    [Fact]
    public async Task Batch_WithAPythonStep_IsRefusedWhenPythonIsOff()
    {
        var tools = new TeamTools(_bridge, new ServerConfig { Python = PythonAccess.Off }, Advice, Access.Scopes, Access.Sessions());

        var refused = JsonNode.Parse((await tools.BatchAsync(new JsonArray(new JsonObject { ["command"] = "python", ["params"] = new JsonObject { ["code"] = "1" } }))).Text())!;
        (await tools.BatchAsync(new JsonArray(new JsonObject { ["command"] = "add_primitive", ["params"] = new JsonObject { ["kind"] = "cube" } }))).Text();

        Assert.False(refused["ok"]!.GetValue<bool>());
        Assert.Single(_bridge.Sent);
    }
}
