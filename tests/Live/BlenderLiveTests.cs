using Snail.MCP.Blender.Adapters.AddOn;
using Snail.MCP.Blender.Adapters.Blender;
using Snail.MCP.Blender.Configuration;

namespace Snail.MCP.Blender.Tests.Live;

/// <summary>The acceptance walk of the specification against a real Blender: model, edit, modify, shade, animate, render, exchange files; and the rest of the base layer: text, empties and collections, scenes and snapshots. One Blender per test class keeps it under a minute.</summary>
[Collection("Loopback")]
public sealed class BlenderLiveTests : IAsyncLifetime
{
    private HeadlessBlender _blender = null!;
    private BlenderConnection _link = null!;
    private string _workDirectory = null!;

    public async Task InitializeAsync()
    {
        if (BlenderInstallation.Find() is null)
        {
            return;
        }

        _blender = await HeadlessBlender.StartAsync();
        _link = new BlenderConnection(_blender.Link, NullLogger<BlenderConnection>.Instance);
        _workDirectory = Path.Combine(Path.GetTempPath(), "snail-blender-live", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_workDirectory);
    }

    public async Task DisposeAsync()
    {
        if (_link is not null) await _link.DisposeAsync();

        _blender?.Dispose();

        if (_workDirectory is not null && Directory.Exists(_workDirectory)) Directory.Delete(_workDirectory, recursive: true);
    }

    [BlenderFact]
    public async Task Ping_RealAddOn_ReportsTheBlenderVersion()
    {
        var reply = await Send(BridgeCommands.Ping);

        Assert.Matches(@"^\d+\.\d+", reply["blender"]!.ToString());
        Assert.False(reply["busy"]!.GetValue<bool>());
    }

    /// <summary>Ping is answered from the socket thread, so a Blender whose dispatcher died answers it like a healthy one; the pulse is what
    /// tells them apart, and it has to move on its own while nothing is asked of Blender.</summary>
    [BlenderFact]
    public async Task Ping_RealAddOn_ReportsAPulseThatKeepsMoving()
    {
        var first = await Send(BridgeCommands.Ping);
        await Task.Delay(TimeSpan.FromMilliseconds(300));
        var second = await Send(BridgeCommands.Ping);

        Assert.True(first["pumped_s_ago"]!.GetValue<double>() < 5, $"the queue was last drained {first["pumped_s_ago"]} s ago");
        Assert.True(second["pumped_s_ago"]!.GetValue<double>() < 5, $"the queue was last drained {second["pumped_s_ago"]} s ago");
        Assert.NotEqual(first["pumped_s_ago"]!.GetValue<double>(), second["pumped_s_ago"]!.GetValue<double>());
        Assert.Null(second["executing"]?.GetValue<string>());
    }

    /// <summary>Opening a project is the first thing a client does over HTTP, and after it a Blender without an interface has no window — so
    /// the selection has to be read from the view layer, which is there either way. Read from the context, scene_info raised AttributeError
    /// on every call after the first file was opened.</summary>
    [BlenderFact]
    public async Task SceneInfo_AfterAProjectIsOpened_StillReportsTheSelection()
    {
        await Send(BridgeCommands.OpenProject, new JsonObject { ["name"] = "selection", ["discard"] = true }, TimeSpan.FromSeconds(60));
        await Send(BridgeCommands.AddPrimitive, new JsonObject { ["kind"] = "cube", ["name"] = "Crate" });

        var scene = await Send(BridgeCommands.SceneInfo);
        var selected = await Send(BridgeCommands.SelectObjects, new JsonObject { ["names"] = new JsonArray("Crate"), ["mode"] = "replace" });

        Assert.Equal(["Crate"], scene["selected"]!.AsArray().Select(name => name!.ToString()));
        Assert.Equal(["Crate"], selected["selected"]!.AsArray().Select(name => name!.ToString()));
    }

    /// <summary>A step runs on Blender's main thread, and the file and heartbeat commands are answered beside it: asked for as a step, they used
    /// to come back as unknown, with a hint that the add-on was older than the server — which was false and sent the reader to reinstall it.</summary>
    [BlenderFact]
    public async Task Run_AskedForACommandAnsweredOffTheMainThread_SaysSoRatherThanCallingItUnknown()
    {
        var refused = await _link.SendAsync(BridgeCommands.Run, new JsonObject
        {
            ["commands"] = new JsonArray(new JsonObject { ["command"] = BridgeCommands.FileList.Name, ["params"] = new JsonObject { ["area"] = VolumeAreas.Files, ["path"] = "." } }),
        });

        Assert.Equal("BadRequest", refused.Error?.Type);
        Assert.Contains("beside Blender's main thread", refused.Error!.Message, StringComparison.Ordinal);
        Assert.Contains("file_list", refused.Error.Details!["commands"]!.AsArray().Select(name => name!.ToString()));
    }

    /// <summary>The freshness check is only worth anything if both sides hash the same thing the same way; here the real add-on's digest meets the server's.</summary>
    [BlenderFact]
    public async Task Ping_RealAddOn_ReportsTheBuildTheServerShipsAndItsWholeRegistry()
    {
        var bundled = new AddOnPackager(new ServerConfig()).Describe();

        var ping = await Send(BridgeCommands.Ping, new JsonObject { ["commands"] = true });

        Assert.Equal(bundled.Digest, ping["addon"]!["digest"]!.ToString());
        Assert.Equal(bundled.Version, ping["addon"]!["version"]!.ToString());
        Assert.Equal(BridgeCommands.All.Count, ping["addon"]!["commands"]!.GetValue<int>());
        Assert.Equal(
            BridgeCommands.All.Select(command => command.Name).Order(),
            ping["addon"]!["command_names"]!.AsArray().Select(name => name!.ToString()).Order());
        Assert.Equal(AddOnProtocol.Version, ping["addon"]!["protocol"]!.GetValue<int>());
        Assert.Equal(
            AddOnProtocol.Required.Order(StringComparer.Ordinal),
            ping["addon"]!["capabilities"]!.AsArray().Select(name => name!.ToString()).Order(StringComparer.Ordinal));
    }

    [BlenderFact]
    public async Task Modeling_PrimitiveExtrudeModifierApply_ChangesTheMesh()
    {
        var cube = await Send(BridgeCommands.AddPrimitive, new JsonObject { ["kind"] = "cube", ["name"] = "Tower", ["location"] = new JsonArray(0, 0, 1) });
        Assert.Equal("Tower", cube["name"]!.ToString());

        var selected = await Send(BridgeCommands.MeshSelect, new JsonObject { ["name"] = "Tower", ["mode"] = "by_normal", ["axis"] = "+z" });
        Assert.Equal(1, selected["selected"]!["faces"]!.GetValue<int>());

        var extruded = await Send(BridgeCommands.MeshExtrude, new JsonObject { ["name"] = "Tower", ["distance"] = 2 });
        Assert.Equal(10, extruded["faces"]!.GetValue<int>());

        var modifier = await Send(BridgeCommands.AddModifier, new JsonObject { ["name"] = "Tower", ["type"] = "SUBSURF", ["settings"] = new JsonObject { ["levels"] = 1 } });
        Assert.Equal(1, modifier["settings"]!["levels"]!.GetValue<int>());

        var applied = await Send(BridgeCommands.ApplyModifier, new JsonObject { ["name"] = "Tower", ["modifier"] = modifier["name"]!.ToString() });
        Assert.True(applied["faces"]!.GetValue<int>() > 10);
        Assert.Empty(applied["modifiers"]!.AsArray());

        var described = await Send(BridgeCommands.DescribeOperator, new JsonObject { ["name"] = "mesh.primitive_cube_add" });
        Assert.Contains(described["properties"]!.AsArray(), property => property!["name"]!.ToString() == "size");
    }

    [BlenderFact]
    public async Task Materials_PrincipledAssignedAndTextured_ReadBackFromTheGraph()
    {
        await Send(BridgeCommands.AddPrimitive, new JsonObject { ["kind"] = "uv_sphere", ["name"] = "Ball" });

        var material = await Send(BridgeCommands.CreateMaterial, new JsonObject
        {
            ["name"] = "Gold",
            ["assign_to"] = "Ball",
            ["base_color"] = new JsonArray(1, 0.8, 0.2),
            ["metallic"] = 1,
            ["roughness"] = 0.3,
        });
        Assert.Equal(1, material["principled"]!["metallic"]!.GetValue<double>());
        Assert.Equal("Gold", material["assigned"]!["material"]!.ToString());

        var textured = await Send(BridgeCommands.ProceduralTexture, new JsonObject { ["material"] = "Gold", ["kind"] = "noise", ["connect_to"] = "Roughness", ["color_ramp"] = true });
        Assert.Contains("Roughness", textured["link"]!.ToString(), StringComparison.Ordinal);

        var info = await Send(BridgeCommands.MaterialInfo, new JsonObject { ["name"] = "Gold" });
        Assert.Contains(info["graph"]!["nodes"]!.AsArray(), node => node!["type"]!.ToString() == "ShaderNodeTexNoise");
        Assert.NotNull(info["principled"]!["roughness"]!["linked_from"]);
    }

    [BlenderFact]
    public async Task Animation_TwoKeysAndCurves_AreListedWithLinearInterpolation()
    {
        await Send(BridgeCommands.AddPrimitive, new JsonObject { ["kind"] = "cube", ["name"] = "Mover" });
        await Send(BridgeCommands.InsertKeyframe, new JsonObject { ["name"] = "Mover", ["frame"] = 1, ["values"] = new JsonObject { ["location"] = new JsonArray(0, 0, 0) }, ["interpolation"] = "LINEAR" });
        var second = await Send(BridgeCommands.InsertKeyframe, new JsonObject { ["name"] = "Mover", ["frame"] = 24, ["values"] = new JsonObject { ["location"] = new JsonArray(4, 0, 0), ["rotation_euler"] = new JsonArray(0, 0, 90) } });
        Assert.Equal(9, second["keyframes"]!.GetValue<int>());

        var listed = await Send(BridgeCommands.ListKeyframes, new JsonObject { ["name"] = "Mover", ["channels"] = new JsonArray("location") });
        var x = listed["curves"]!.AsArray().Single(curve => curve!["index"]!.GetValue<int>() == 0)!;
        Assert.Equal(2, x["total"]!.GetValue<int>());
        Assert.Equal("LINEAR", x["keyframes"]![0]!["interpolation"]!.ToString());

        await Send(BridgeCommands.SetFrame, new JsonObject { ["frame"] = 24 });
        var info = await Send(BridgeCommands.ObjectInfo, new JsonObject { ["name"] = "Mover" });
        Assert.Equal(4, info["location"]![0]!.GetValue<double>(), 3);
        Assert.Equal(90, info["rotation_euler"]![2]!.GetValue<double>(), 1);
    }

    [BlenderFact]
    public async Task Rendering_SmallEeveeFrame_WritesAPngFile()
    {
        await Send(BridgeCommands.AddPrimitive, new JsonObject { ["kind"] = "monkey", ["name"] = "Subject" });
        await Send(BridgeCommands.AddCamera, new JsonObject { ["location"] = new JsonArray(0, -6, 2), ["look_at"] = new JsonArray(0, 0, 0) });
        await Send(BridgeCommands.AddLight, new JsonObject { ["type"] = "SUN", ["energy"] = 3 });
        var settings = await Send(BridgeCommands.RenderSettings, new JsonObject { ["engine"] = "EEVEE", ["resolution_x"] = 160, ["resolution_y"] = 120, ["samples"] = 4 });
        Assert.StartsWith("BLENDER_EEVEE", settings["engine"]!.ToString(), StringComparison.Ordinal);

        var path = Path.Combine(_workDirectory, "frame.png");
        var rendered = await Send(BridgeCommands.RenderImage, new JsonObject { ["path"] = path }, TimeSpan.FromSeconds(180));

        Assert.True(File.Exists(rendered["path"]!.ToString()));
        Assert.True(rendered["bytes"]!.GetValue<long>() > 1000);
    }

    [BlenderFact]
    public async Task Io_ExportFourFormatsAndImportOneBack_RoundTrips()
    {
        await Send(BridgeCommands.AddPrimitive, new JsonObject { ["kind"] = "cylinder", ["name"] = "Part" });

        foreach (var extension in new[] { "glb", "obj", "stl", "fbx" })
        {
            var path = Path.Combine(_workDirectory, $"part.{extension}");
            var exported = await Send(BridgeCommands.ExportFile, new JsonObject { ["path"] = path, ["selected_only"] = true, ["names"] = new JsonArray("Part") });

            Assert.True(File.Exists(path), extension);
            Assert.True(exported["bytes"]!.GetValue<long>() > 100, extension);
        }

        await Send(BridgeCommands.DeleteObjects, new JsonObject { ["names"] = new JsonArray("Part") });
        var imported = await Send(BridgeCommands.ImportFile, new JsonObject { ["path"] = Path.Combine(_workDirectory, "part.obj") });

        Assert.Equal(1, imported["count"]!.GetValue<int>());
    }

    [BlenderFact]
    public async Task Objects_DuplicatedSelectedAndReparented_AreListedByTheScene()
    {
        await Send(BridgeCommands.AddPrimitive, new JsonObject { ["kind"] = "cube", ["name"] = "Origin" });

        var copy = await Send(BridgeCommands.DuplicateObject, new JsonObject { ["name"] = "Origin", ["new_name"] = "Clone", ["offset"] = new JsonArray(2, 0, 0) });
        Assert.Equal("Clone", copy["name"]!.ToString());
        Assert.Equal(2, copy["location"]![0]!.GetValue<double>(), 3);

        var selected = await Send(BridgeCommands.SelectObjects, new JsonObject { ["mode"] = "replace", ["names"] = new JsonArray("Origin", "Clone") });
        Assert.Equal(["Clone", "Origin"], selected["selected"]!.AsArray().Select(name => name!.ToString()).Order());

        var updated = await Send(BridgeCommands.UpdateObject, new JsonObject { ["name"] = "Clone", ["new_name"] = "Child", ["parent"] = "Origin", ["hide_render"] = true });
        Assert.Equal("Child", updated["name"]!.ToString());
        Assert.Equal("Origin", updated["parent"]!.ToString());

        var listed = await Send(BridgeCommands.ListObjects, new JsonObject { ["type"] = "MESH", ["name_contains"] = "hil" });
        Assert.Equal(1, listed["total"]!.GetValue<int>());
        Assert.Equal("Child", listed["objects"]![0]!["name"]!.ToString());
    }

    [BlenderFact]
    public async Task CameraAndLight_ChangedAfterTheyExist_ReportTheNewSettings()
    {
        var camera = await Send(BridgeCommands.AddCamera, new JsonObject { ["name"] = "Shot", ["location"] = new JsonArray(0, -5, 2) });

        var lensed = await Send(BridgeCommands.SetCamera, new JsonObject { ["name"] = camera["name"]!.ToString(), ["lens"] = 85, ["make_active"] = true });
        Assert.Equal(85, lensed["lens"]!.GetValue<double>(), 1);
        Assert.True(lensed["active"]!.GetValue<bool>());

        await Send(BridgeCommands.AddLight, new JsonObject { ["type"] = "POINT", ["name"] = "Lamp", ["energy"] = 100 });

        var brighter = await Send(BridgeCommands.SetLight, new JsonObject { ["name"] = "Lamp", ["energy"] = 250, ["color"] = new JsonArray(1, 0.5, 0.2) });
        Assert.Equal(250, brighter["energy"]!.GetValue<double>(), 1);
        Assert.Equal(0.5, brighter["color"]![1]!.GetValue<double>(), 2);
    }

    [BlenderFact]
    public async Task Files_SavedReopenedAndSteppedBack_ComeBackWithTheScene()
    {
        var path = Path.Combine(_workDirectory, "scene.blend");
        await Send(BridgeCommands.AddPrimitive, new JsonObject { ["kind"] = "torus", ["name"] = "Kept" });
        await Send(BridgeCommands.SaveFile, new JsonObject { ["path"] = path });
        await Send(BridgeCommands.NewFile);

        var opened = await Send(BridgeCommands.OpenFile, new JsonObject { ["path"] = path }, TimeSpan.FromSeconds(60));
        Assert.Equal(path, opened["path"]!.ToString());

        var listed = await Send(BridgeCommands.ListObjects, new JsonObject { ["name_contains"] = "Kept" });
        Assert.Equal(1, listed["total"]!.GetValue<int>());

        var stepped = await Send(BridgeCommands.UndoRedo, new JsonObject { ["action"] = "undo", ["steps"] = 1 });
        Assert.Equal("undo", stepped["action"]!.ToString());
    }

    [BlenderFact]
    public async Task Progress_WithNothingRendering_ReportsAnIdleBlender()
    {
        var progress = await Send(BridgeCommands.Progress);

        Assert.False(progress["active"]!.GetValue<bool>());
        Assert.Equal(0, progress["frames_done"]!.GetValue<int>());
        Assert.Empty(progress["files"]!.AsArray());
    }

    [BlenderFact]
    public async Task Errors_UnknownOperatorAndBadParameter_ComeBackTyped()
    {
        var unknown = await _link.SendAsync(BridgeCommands.RunOperator, new JsonObject { ["name"] = "mesh.no_such_thing" });
        Assert.Equal("UnknownOperator", unknown.Error!.Type);

        var badParameter = await _link.SendAsync(BridgeCommands.RunOperator, new JsonObject { ["name"] = "mesh.primitive_cube_add", ["params"] = new JsonObject { ["sizee"] = 2 } });
        Assert.Equal("UnknownParameter", badParameter.Error!.Type);
        Assert.Contains("size", badParameter.Error.Details!["known"]!.AsArray().Select(node => node!.ToString()));

        var python = await Send(BridgeCommands.Python, new JsonObject { ["code"] = "print('hi'); result = len(bpy.data.objects)" });
        Assert.Equal("hi\n", python["stdout"]!.ToString());
    }

    [BlenderFact]
    public async Task TextEmptyAndCollection_AreCreatedAndUpdated()
    {
        var text = await Send(BridgeCommands.AddText, new JsonObject { ["body"] = "Snail", ["size"] = 0.5, ["extrude"] = 0.05 });
        Assert.Equal("FONT", text["type"]!.ToString());
        Assert.Equal(90, text["rotation_euler"]![0]!.GetValue<double>(), 1);

        var empty = await Send(BridgeCommands.AddEmpty, new JsonObject { ["type"] = "SPHERE", ["name"] = "Pivot", ["size"] = 0.5 });
        Assert.Equal("SPHERE", empty["display"]!.ToString());

        await Send(BridgeCommands.CreateCollection, new JsonObject { ["name"] = "Props" });
        await Send(BridgeCommands.MoveToCollection, new JsonObject { ["names"] = new JsonArray("Pivot"), ["collection"] = "Props" });
        var updated = await Send(BridgeCommands.UpdateCollection, new JsonObject { ["name"] = "Props", ["hide_render"] = true, ["exclude"] = true });
        Assert.True(updated["hide_render"]!.GetValue<bool>());
        Assert.True(updated["excluded"]!.GetValue<bool>());
    }

    [BlenderFact]
    public async Task ScenesAndSnapshots_CreateRenameRestore()
    {
        var created = await Send(BridgeCommands.Scenes, new JsonObject { ["action"] = "create", ["name"] = "Shot02", ["copy"] = "LINK_COPY" });
        Assert.Equal(3, created["objects"]!.GetValue<int>());

        var listed = await Send(BridgeCommands.Scenes);
        Assert.Equal(2, listed["scenes"]!.AsArray().Count);

        var snapshots = Path.Combine(_workDirectory, "snapshots");
        var saved = await Send(BridgeCommands.Snapshot, new JsonObject { ["action"] = "save", ["name"] = "before", ["note"] = "two scenes", ["directory"] = snapshots });
        Assert.True(saved["bytes"]!.GetValue<long>() > 1000);

        await Send(BridgeCommands.Scenes, new JsonObject { ["action"] = "remove", ["name"] = "Shot02" });
        var restored = await Send(BridgeCommands.Snapshot, new JsonObject { ["action"] = "restore", ["name"] = "before", ["directory"] = snapshots }, TimeSpan.FromSeconds(60));
        Assert.True(restored["restored"]!.GetValue<bool>());

        var after = await Send(BridgeCommands.Scenes);
        Assert.Equal(2, after["scenes"]!.AsArray().Count);
    }

    private async Task<JsonNode> Send(BridgeCommand command, JsonObject? parameters = null, TimeSpan? timeout = null)
    {
        var reply = await _link.SendAsync(command, parameters, timeout);

        Assert.True(reply.IsOk, $"{command.Name}: {reply.Error?.Type} {reply.Error?.Message} {reply.Error?.Details}");

        return reply.Result!;
    }
}
