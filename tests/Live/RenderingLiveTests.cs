using ModelContextProtocol;
using Snail.MCP.Blender.Adapters.Blender;
using Snail.MCP.Blender.Application.Rendering;

namespace Snail.MCP.Blender.Tests.Live;

/// <summary>The rendering skill against a real Blender: a render with its preview and its progress, colour management and output, engines, view layers and passes, the physical camera and camera moves, light rigs, object flags, the world, a product shot and a bake.</summary>
public sealed class RenderingLiveTests : IAsyncLifetime
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
    public async Task RenderImage_ReturnsAPreviewWithExposureStatistics()
    {
        await Send(BridgeCommands.RenderSettings, new JsonObject { ["engine"] = "EEVEE", ["resolution_x"] = 96, ["resolution_y"] = 64, ["samples"] = 1 });

        var rendered = await Send(BridgeCommands.RenderImage, new JsonObject { ["path"] = Path.Combine(_workDirectory, "still.png") }, TimeSpan.FromSeconds(120));

        Assert.Equal("image/jpeg", rendered["preview"]!["mime"]!.ToString());
        Assert.True(rendered["preview"]!["base64"]!.ToString().Length > 100);
        Assert.Equal(96, rendered["preview"]!["width"]!.GetValue<int>());
        Assert.InRange(rendered["stats"]!["mean_luminance"]!.GetValue<double>(), 0.01, 0.99);
        Assert.Equal(8, rendered["stats"]!["histogram"]!.AsArray().Count);

        var inspected = await Send(BridgeCommands.InspectImage, new JsonObject { ["path"] = rendered["path"]!.ToString(), ["preview"] = false });
        Assert.Equal([96, 64], inspected["size"]!.AsArray().Select(node => node!.GetValue<int>()));
        Assert.Null(inspected["preview"]);
    }

    [BlenderFact]
    public async Task Progress_OnTheMonitorLink_ReportsFramesWhileTheMainLinkRenders()
    {
        await Send(BridgeCommands.RenderSettings, new JsonObject { ["engine"] = "EEVEE", ["resolution_x"] = 64, ["resolution_y"] = 64, ["samples"] = 1 });
        await using var links = new BlenderLinks(_blender.Link, NullLogger<BlenderConnection>.Instance);
        var monitor = new BlenderMonitor(links);
        var watch = new RenderWatch(monitor) { Interval = TimeSpan.FromMilliseconds(100) };
        var reported = new List<ProgressNotificationValue>();
        var progress = new Progress<ProgressNotificationValue>(reported.Add);

        var render = _link.SendAsync(BridgeCommands.RenderAnimation, new JsonObject { ["output_path"] = Path.Combine(_workDirectory, "anim_####"), ["start"] = 1, ["end"] = 24, ["preview"] = false }, TimeSpan.FromSeconds(180));
        var reply = await watch.FollowAsync(render, progress, 24, CancellationToken.None);
        await Task.Delay(50);

        Assert.True(reply.IsOk, reply.Error?.Message);
        Assert.NotEmpty(reported);
        Assert.Contains(reported, value => value.Total == 24 && value.Message!.Contains("rendering", StringComparison.Ordinal));

        var idle = await monitor.ProbeAsync();
        Assert.False(idle.Result!["active"]!.GetValue<bool>());
        Assert.Equal(24, idle.Result!["frames_done"]!.GetValue<int>());
    }

    [BlenderFact]
    public async Task ColorManagement_AgXPunchy_IsAppliedAndUnknownTransformsAreListed()
    {
        var applied = await Send(BridgeCommands.SetColorManagement, new JsonObject { ["view_transform"] = "AgX", ["look"] = "Punchy", ["exposure"] = 0.5 });

        Assert.Equal("AgX - Punchy", applied["look"]!.ToString());
        Assert.Equal(0.5, applied["exposure"]!.GetValue<double>());
        Assert.Contains("Filmic", applied["known"]!["view_transforms"]!.AsArray().Select(node => node!.ToString()));

        var refused = await _link.SendAsync(BridgeCommands.SetColorManagement, new JsonObject { ["view_transform"] = "Nope" });

        Assert.False(refused.IsOk);
        Assert.Equal("BadRequest", refused.Error!.Type);
        Assert.Contains("AgX", refused.Error.Details!["known"]!.AsArray().Select(node => node!.ToString()));
    }

    [BlenderFact]
    public async Task Output_FullFloatExrWithStampAndRegion_ReportsTheFirstFile()
    {
        var output = await Send(BridgeCommands.SetOutput, new JsonObject
        {
            ["path"] = Path.Combine(_workDirectory, "beauty_####"),
            ["file_format"] = "OPEN_EXR",
            ["color_depth"] = 32,
            ["exr_codec"] = "DWAA",
            ["overwrite"] = false,
            ["placeholder"] = true,
            ["stamp"] = new JsonObject { ["enabled"] = true, ["note"] = "v01", ["frame"] = true },
            ["region"] = new JsonArray(0.1, 0.1, 0.9, 0.9),
        });

        Assert.Equal("32", output["color_depth"]!.ToString());
        Assert.Equal("DWAA", output["exr_codec"]!.ToString());
        Assert.EndsWith("beauty_0001.exr", output["first_file"]!.ToString());
        Assert.False(output["overwrite"]!.GetValue<bool>());
        Assert.Contains("frame", output["stamp"]!["fields"]!.AsArray().Select(node => node!.ToString()));
        Assert.Equal(0.9, output["region"]![2]!.GetValue<double>(), 3);
    }

    [BlenderFact]
    public async Task Output_PresetStereoAndFreestyle_AreApplied()
    {
        var output = await Send(BridgeCommands.SetOutput, new JsonObject { ["preset"] = "DCI_4K", ["stereo"] = new JsonObject { ["enabled"] = true, ["mode"] = "STEREO_3D", ["interocular_distance"] = 0.065 } });

        Assert.Equal([4096, 2160, 100], output["resolution"]!.AsArray().Select(node => node!.GetValue<int>()));
        Assert.True(output["stereo"]!["enabled"]!.GetValue<bool>());
        Assert.Equal(0.065, output["stereo"]!["interocular_distance"]!.GetValue<double>(), 3);

        var freestyle = await Send(BridgeCommands.SetFreestyle, new JsonObject { ["enabled"] = true, ["line_thickness"] = 2.5, ["linesets"] = new JsonArray(new JsonObject { ["name"] = "Ink", ["edges"] = new JsonArray("silhouette", "crease"), ["thickness"] = 3 }) });

        Assert.True(freestyle["enabled"]!.GetValue<bool>());
        Assert.Contains(freestyle["linesets"]!.AsArray(), lineset => lineset!["name"]!.ToString() == "Ink" && lineset["thickness"]!.GetValue<double>() == 3);
    }

    [BlenderFact]
    public async Task Engines_CyclesEeveeAndMotionBlur_ReportEffectiveSettings()
    {
        var cycles = await Send(BridgeCommands.SetCycles, new JsonObject { ["samples"] = 32, ["adaptive_threshold"] = 0.05, ["time_limit"] = 5, ["denoiser"] = "OPENIMAGEDENOISE", ["bounces"] = new JsonObject { ["total"] = 6, ["diffuse"] = 2 }, ["clamp"] = new JsonObject { ["indirect"] = 8 }, ["persistent_data"] = true });

        Assert.Equal("CYCLES", cycles["engine"]!.ToString());
        Assert.Equal(6, cycles["bounces"]!["total"]!.GetValue<int>());
        Assert.Equal(8, cycles["clamp"]!["indirect"]!.GetValue<double>());
        Assert.True(cycles["persistent_data"]!.GetValue<bool>());

        var eevee = await Send(BridgeCommands.SetEevee, new JsonObject { ["samples"] = 8, ["shadow_rays"] = 2, ["raytracing"] = true, ["raytracing_options"] = new JsonObject { ["denoise"] = true }, ["volumetrics"] = new JsonObject { ["samples"] = 32 } });

        Assert.StartsWith("BLENDER_EEVEE", eevee["engine"]!.ToString());
        Assert.Equal(2, eevee["shadow_rays"]!.GetValue<int>());
        Assert.Equal(32, eevee["volumetrics"]!["samples"]!.GetValue<int>());

        var blur = await Send(BridgeCommands.SetMotionBlur, new JsonObject { ["enabled"] = true, ["shutter"] = 0.25, ["position"] = "START", ["steps"] = 4 });

        Assert.True(blur["enabled"]!.GetValue<bool>());
        Assert.Equal("START", blur["position"]!.ToString());
        Assert.Equal(4, blur["steps"]!.GetValue<int>());
    }

    [BlenderFact]
    public async Task ViewLayers_CryptomatteAovsLightGroupsAndLinking_AreConfigured()
    {
        await Send(BridgeCommands.AddPrimitive, new JsonObject { ["kind"] = "uv_sphere", ["name"] = "Hero" });

        var layer = await Send(BridgeCommands.ViewLayer, new JsonObject
        {
            ["action"] = "create",
            ["name"] = "Fg",
            ["passes"] = new JsonObject { ["z"] = true, ["shadow_catcher"] = true },
            ["cryptomatte"] = new JsonObject { ["object"] = true, ["material"] = true, ["levels"] = 4 },
            ["aovs"] = new JsonArray("Mask", new JsonObject { ["name"] = "Wet", ["type"] = "VALUE" }),
            ["light_groups"] = new JsonArray("Key"),
            ["light_group_members"] = new JsonObject { ["Key"] = new JsonArray("Light") },
        });

        Assert.Equal("Fg", layer["name"]!.ToString());
        Assert.True(layer["cryptomatte"]!["material"]!.GetValue<bool>());
        Assert.Equal(4, layer["cryptomatte"]!["levels"]!.GetValue<int>());
        Assert.Equal("VALUE", layer["aovs"]![1]!["type"]!.ToString());
        Assert.Equal(["Key"], layer["light_groups"]!.AsArray().Select(node => node!.ToString()));
        Assert.Contains("shadow_catcher", layer["passes"]!.AsArray().Select(node => node!.ToString()));

        var excluded = await Send(BridgeCommands.ViewLayer, new JsonObject { ["name"] = "Fg", ["collections"] = new JsonObject { ["exclude"] = new JsonArray("Collection") } });
        Assert.True(excluded["collections"]!["children"]![0]!["exclude"]!.GetValue<bool>());

        var linking = await Send(BridgeCommands.LightLinking, new JsonObject { ["light"] = "Light", ["receivers"] = new JsonArray("Hero"), ["blockers"] = new JsonArray("Cube"), ["blocker_mode"] = "exclude" });
        Assert.Equal("INCLUDE", linking["receivers"]![0]!["state"]!.ToString());
        Assert.Equal("EXCLUDE", linking["blockers"]![0]!["state"]!.ToString());

        var removed = await Send(BridgeCommands.ViewLayer, new JsonObject { ["action"] = "remove", ["name"] = "Fg" });
        Assert.Equal(["ViewLayer"], removed["layers"]!.AsArray().Select(node => node!.ToString()));
    }

    [BlenderFact]
    public async Task RenderPasses_SwitchedOn_AreReportedByTheViewLayer()
    {
        var passes = await Send(BridgeCommands.RenderPasses, new JsonObject { ["passes"] = new JsonObject { ["z"] = true, ["normal"] = true } });

        Assert.True(passes["passes"]!["z"]!.GetValue<bool>());
        Assert.True(passes["passes"]!["normal"]!.GetValue<bool>());
    }

    [BlenderFact]
    public async Task CameraOptics_AnamorphicApertureFocusAndShutterCurve_AreApplied()
    {
        var optics = await Send(BridgeCommands.SetCameraOptics, new JsonObject
        {
            ["sensor"] = new JsonObject { ["width"] = 24.89, ["fit"] = "HORIZONTAL" },
            ["lens"] = 35,
            ["shift"] = new JsonArray(0.1, 0),
            ["aperture"] = new JsonObject { ["fstop"] = 2.0, ["blades"] = 7, ["rotation"] = 15, ["ratio"] = 2.0 },
            ["focus"] = new JsonObject { ["object"] = "Cube" },
            ["object_motion_blur"] = new JsonObject { ["Cube"] = new JsonObject { ["enabled"] = true, ["steps"] = 3 } },
            ["shutter_curve"] = new JsonArray(new JsonArray(0, 0), new JsonArray(0.1, 1), new JsonArray(0.9, 1), new JsonArray(1, 0)),
        });

        Assert.Equal(7, optics["aperture"]!["blades"]!.GetValue<int>());
        Assert.Equal(2.0, optics["aperture"]!["ratio"]!.GetValue<double>());
        Assert.Equal("Cube", optics["focus"]!["object"]!.ToString());
        Assert.Equal(3, optics["object_motion_blur"]!["Cube"]!["steps"]!.GetValue<int>());
        Assert.Equal(4, optics["shutter_curve"]!.AsArray().Count);
        Assert.True(optics["hyperfocal_m"]!.GetValue<double>() > 10);

        var refused = await _link.SendAsync(BridgeCommands.SetCameraOptics, new JsonObject { ["aperture"] = new JsonObject { ["blades"] = 2 } });
        Assert.Equal("BadRequest", refused.Error!.Type);
    }

    [BlenderFact]
    public async Task CameraMoves_TurntableAndOrbit_KeyTheirPivots()
    {
        var turntable = await Send(BridgeCommands.CameraMove, new JsonObject { ["type"] = "turntable", ["target"] = "Cube", ["start"] = 1, ["end"] = 48 });
        Assert.Equal("Turntable", turntable["pivot"]!.ToString());

        var keys = await Send(BridgeCommands.ListKeyframes, new JsonObject { ["name"] = "Turntable" });
        Assert.NotEmpty(keys["curves"]!.AsArray());

        var orbit = await Send(BridgeCommands.CameraMove, new JsonObject { ["type"] = "orbit", ["target"] = "Cube", ["start"] = 1, ["end"] = 48, ["height"] = 1 });
        Assert.Equal("Camera", orbit["camera"]!.ToString());
        Assert.Equal(1, orbit["height"]!.GetValue<double>());

        var refused = await _link.SendAsync(BridgeCommands.CameraMove, new JsonObject { ["type"] = "spiral", ["target"] = "Cube" });
        Assert.Equal("BadRequest", refused.Error!.Type);
    }

    [BlenderFact]
    public async Task Lights_KelvinShapeAndRig_AreBuiltAroundTheSubject()
    {
        var warm = await Send(BridgeCommands.AddLight, new JsonObject { ["type"] = "AREA", ["name"] = "Warm", ["temperature"] = 3200, ["shape"] = "RECTANGLE", ["size"] = 2, ["size_y"] = 0.5, ["spread"] = 90 });

        Assert.Equal(3200, warm["kelvin"]!.GetValue<double>());
        Assert.Equal("RECTANGLE", warm["shape"]!.ToString());
        Assert.True(warm["color"]![2]!.GetValue<double>() < warm["color"]![0]!.GetValue<double>());

        var rig = await Send(BridgeCommands.LightRig, new JsonObject { ["target"] = "Cube", ["key_energy"] = 800 });

        Assert.Equal(["key", "fill", "rim"], rig["lights"]!.AsArray().Select(light => light!["role"]!.ToString()));
        Assert.Equal(280, rig["lights"]![1]!["energy"]!.GetValue<double>());
    }

    [BlenderFact]
    public async Task ObjectRenderFlags_ShadowCatcherAndRayVisibility_AreReported()
    {
        var flags = await Send(BridgeCommands.ObjectRenderFlags, new JsonObject { ["names"] = new JsonArray("Collection"), ["shadow_catcher"] = true, ["visible"] = new JsonObject { ["glossy"] = false }, ["pass_index"] = 3, ["light_group"] = "Key" });

        var cube = flags["objects"]!.AsArray().Single(item => item!["name"]!.ToString() == "Cube")!;
        Assert.True(cube["shadow_catcher"]!.GetValue<bool>());
        Assert.False(cube["visible"]!["glossy"]!.GetValue<bool>());
        Assert.Equal(3, cube["pass_index"]!.GetValue<int>());
        Assert.Equal("Key", cube["light_group"]!.ToString());
    }

    [BlenderFact]
    public async Task World_ColorAndStrength_AreApplied()
    {
        var world = await Send(BridgeCommands.SetWorld, new JsonObject { ["color"] = new JsonArray(0.2, 0.3, 0.5), ["strength"] = 2, ["mist"] = true, ["mist_depth"] = 40 });

        Assert.Equal(2, world["strength"]!.GetValue<double>());
        Assert.Equal(0.5, world["color"]![2]!.GetValue<double>(), 3);
        Assert.True(world["mist"]!["enabled"]!.GetValue<bool>());
    }

    [BlenderFact]
    public async Task RenderObject_LoneCube_WritesATransparentShotAndRestoresTheScene()
    {
        await Send(BridgeCommands.AddPrimitive, new JsonObject { ["kind"] = "cube", ["name"] = "Product" });
        await Send(BridgeCommands.AddPrimitive, new JsonObject { ["kind"] = "plane", ["name"] = "Floor", ["size"] = 20 });
        await Send(BridgeCommands.RenderSettings, new JsonObject { ["engine"] = "EEVEE", ["samples"] = 2 });
        var path = Path.Combine(_workDirectory, "product.png");

        var shot = await Send(BridgeCommands.RenderObject, new JsonObject { ["name"] = "Product", ["path"] = path, ["resolution_x"] = 96, ["resolution_y"] = 96 }, TimeSpan.FromSeconds(180));

        Assert.True(File.Exists(path));
        Assert.True(shot["bytes"]!.GetValue<long>() > 100);
        var scene = await Send(BridgeCommands.SceneInfo);
        Assert.Equal(1920, scene["render"]!["resolution"]![0]!.GetValue<int>());
        Assert.DoesNotContain(scene["objects"]!.AsObject(), pair => pair.Key == "CAMERA" && pair.Value!.GetValue<int>() > 1);
        var floor = await Send(BridgeCommands.ObjectInfo, new JsonObject { ["name"] = "Floor" });
        Assert.False(floor["hide_render"]!.GetValue<bool>());
    }

    [BlenderFact]
    public async Task BakeTexture_DiffuseOfAColouredCube_WritesAPng()
    {
        await Send(BridgeCommands.AddPrimitive, new JsonObject { ["kind"] = "cube", ["name"] = "Baked" });
        await Send(BridgeCommands.CreateMaterial, new JsonObject { ["name"] = "Blue", ["assign_to"] = "Baked", ["base_color"] = new JsonArray(0, 0, 1) });
        await Send(BridgeCommands.UnwrapUv, new JsonObject { ["name"] = "Baked", ["method"] = "smart" });
        var path = Path.Combine(_workDirectory, "baked.png");

        var baked = await Send(BridgeCommands.BakeTexture, new JsonObject { ["name"] = "Baked", ["path"] = path, ["type"] = "DIFFUSE", ["width"] = 64, ["samples"] = 1 }, TimeSpan.FromSeconds(300));

        Assert.True(File.Exists(path));
        Assert.True(baked["bytes"]!.GetValue<long>() > 100);
        var info = await Send(BridgeCommands.MaterialInfo, new JsonObject { ["name"] = "Blue" });
        Assert.DoesNotContain(info["graph"]!["nodes"]!.AsArray(), node => node!["name"]!.ToString() == "Snail Bake Target");
    }

    private async Task<JsonNode> Send(BridgeCommand command, JsonObject? parameters = null, TimeSpan? timeout = null)
    {
        var reply = await _link.SendAsync(command, parameters, timeout);

        Assert.True(reply.IsOk, $"{command.Name}: {reply.Error?.Type} {reply.Error?.Message} {reply.Error?.Details}");

        return reply.Result!;
    }
}
