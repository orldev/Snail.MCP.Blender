using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Protocol;
using Snail.MCP.Blender.Application.Rendering;
using Snail.MCP.Blender.Application.Sessions;
using Snail.MCP.Blender.Configuration;
using Snail.MCP.Blender.Tools;
using Snail.MCP.Blender.Tools.Rendering;

namespace Snail.MCP.Blender.Tests.Tools;

public class RenderingToolsTests
{
    private readonly ScriptedBridge _bridge = new();

    private RenderTools Render() =>
        new(_bridge, new RenderWatch(new SilentMonitor()), new RenderGallery(), new ServerConfig { DataDirectory = "/data/snail" },
            new RenderJobs(new ClientSessions([], [], agent: null, new ServiceCollection().BuildServiceProvider(), TimeProvider.System)));

    [Fact]
    public async Task RenderSettings_ResolutionAboveTheLimit_IsRefusedBeforeReachingBlender()
    {
        var tools = Render();

        var response = JsonNode.Parse((await tools.SettingsAsync(resolutionX: ToolLimits.MaxRenderResolution + 1)).Text())!;

        Assert.False(response["ok"]!.GetValue<bool>());
        Assert.Empty(_bridge.Sent);
    }

    [Fact]
    public async Task RenderSettings_ResolutionAtTheLimit_IsSent()
    {
        var tools = Render();

        (await tools.SettingsAsync(engine: "CYCLES", resolutionX: ToolLimits.MaxRenderResolution, samples: 128)).Text();

        Assert.Equal(BridgeCommands.RenderSettings, _bridge.Sent.Single().Command);
        Assert.Equal(4096, _bridge.LastParameters!["resolution_x"]!.GetValue<int>());
    }

    /// <summary>An animation blocks Blender for as long as it takes and cannot be stopped, so it goes to a second Blender by default — the same range, the same path, and a job that can be cancelled.</summary>
    [Fact]
    public async Task RenderAnimation_ByDefault_StartsABackgroundJob_OverTheSameRangeAndPath()
    {
        _bridge.Answer(BridgeCommands.RenderJob, new JsonObject { ["id"] = "job-1", ["state"] = "queued" });
        var tools = Render();

        var answer = JsonNode.Parse((await tools.AnimationAsync("/renders/shot_", start: 1, end: 48, step: 2, fileFormat: "PNG")).Text())!["data"]!;

        Assert.Equal(BridgeCommands.RenderJob, _bridge.Sent.Single().Command);
        Assert.Equal("/renders/shot_", _bridge.LastParameters!["output_path"]!.ToString());
        Assert.Equal("1-48x2", _bridge.LastParameters["frames"]!.ToString());
        Assert.Equal("PNG", _bridge.LastParameters["file_format"]!.ToString());
        Assert.Equal(1, _bridge.LastParameters["chunks"]!.GetValue<int>());
        Assert.Equal("job-1", answer["id"]!.ToString());
        Assert.Contains("blender_render_job_cancel", answer["note"]!.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task RenderAnimation_WithoutARange_LetsTheJobUseTheScenesOwn()
    {
        _bridge.Answer(BridgeCommands.RenderJob, new JsonObject { ["id"] = "job-2", ["state"] = "queued" });

        (await Render().AnimationAsync("/renders/shot_")).Text();

        Assert.False(_bridge.LastParameters!.ContainsKey("frames"));
    }

    [Fact]
    public async Task RenderAnimation_AskedToBlock_RendersHereWithTheOldParameters()
    {
        _bridge.Answer(BridgeCommands.RenderAnimation, new JsonObject { ["path"] = "/renders/shot_0001.png" });

        (await Render().AnimationAsync("/renders/shot_", start: 1, end: 4, background: false)).Text();

        Assert.Equal(BridgeCommands.RenderAnimation, _bridge.Sent.Single().Command);
        Assert.Equal(1, _bridge.LastParameters!["start"]!.GetValue<int>());
        Assert.Equal(4, _bridge.LastParameters["end"]!.GetValue<int>());
    }

    [Fact]
    public async Task RenderImage_DefaultTimeout_IsLongerThanARegularCall()
    {
        var tools = Render();

        await tools.ImageAsync("/tmp/out.png");

        Assert.Equal(TimeSpan.FromSeconds(300), _bridge.Sent.Single().Timeout);
        Assert.True(_bridge.LastParameters!["preview"]!.GetValue<bool>());
    }

    [Fact]
    public async Task RenderImage_ReplyWithAPreview_BecomesTextAndImageContent()
    {
        _bridge.Answer(BridgeCommands.RenderImage, new JsonObject { ["path"] = "/tmp/out.png", ["stats"] = new JsonObject { ["mean_luminance"] = 0.3 }, ["preview"] = new JsonObject { ["mime"] = "image/jpeg", ["base64"] = "AAAA", ["width"] = 64, ["height"] = 32 } });
        var tools = Render();

        var result = await tools.ImageAsync("/tmp/out.png");

        var text = JsonNode.Parse(((TextContentBlock)result.Content[0]).Text)!;
        var image = Assert.IsType<ImageContentBlock>(result.Content[1]);
        Assert.Equal("AAAA", System.Text.Encoding.UTF8.GetString(image.Data.ToArray()));
        Assert.Equal(3, image.DecodedData.Length);
        Assert.Equal("image/jpeg", image.MimeType);
        Assert.Equal(64, text["data"]!["preview"]!["width"]!.GetValue<int>());
        Assert.Null(text["data"]!["preview"]!["base64"]);
        Assert.Equal(0.3, text["data"]!["stats"]!["mean_luminance"]!.GetValue<double>());
    }

    [Fact]
    public async Task RenderImage_ReplyWithoutAPreview_IsTextOnly()
    {
        _bridge.Answer(BridgeCommands.RenderImage, new JsonObject { ["path"] = "/tmp/out.png" });
        var tools = Render();

        var result = await tools.ImageAsync("/tmp/out.png", preview: false);

        var block = Assert.Single(result.Content);
        Assert.IsType<TextContentBlock>(block);
        Assert.False(_bridge.LastParameters!["preview"]!.GetValue<bool>());
    }

    [Fact]
    public async Task SetOutput_DepthCodecAndStamp_SendWireNames()
    {
        var tools = new OutputTools(_bridge);

        (await tools.SettingsAsync(path: "/renders/beauty_####", fileFormat: "OPEN_EXR", colorDepth: 32, exrCodec: "DWAA", overwrite: false, stamp: new Stamp { Note = "v03", RenderTime = true }, region: [0.1, 0.1, 0.9, 0.9])).Text();

        var parameters = _bridge.LastParameters!;

        Assert.Equal(BridgeCommands.SetOutput, _bridge.Sent.Single().Command);
        Assert.Equal(32, parameters["color_depth"]!.GetValue<int>());
        Assert.Equal("DWAA", parameters["exr_codec"]!.ToString());
        Assert.False(parameters["overwrite"]!.GetValue<bool>());
        Assert.Equal("v03", parameters["stamp"]!["note"]!.ToString());
        Assert.True(parameters["stamp"]!["render_time"]!.GetValue<bool>());
        Assert.Null(parameters["stamp"]!["frame"]);
        Assert.Equal(4, parameters["region"]!.AsArray().Count);
    }

    [Fact]
    public async Task SetOutput_PresetAndStereo_AreForwarded()
    {
        var tools = new OutputTools(_bridge);

        (await tools.SettingsAsync(preset: "DCI_4K", stereo: new Stereo { Enabled = true, InterocularDistance = 0.065 })).Text();

        Assert.Equal("DCI_4K", _bridge.LastParameters!["preset"]!.ToString());
        Assert.Equal(0.065, _bridge.LastParameters["stereo"]!["interocular_distance"]!.GetValue<double>());
    }

    [Fact]
    public async Task SetCycles_BouncesAndDenoiser_KeepTheirGroups()
    {
        var tools = new EngineTools(_bridge);

        (await tools.CyclesAsync(samples: 1024, denoiser: "OPENIMAGEDENOISE", bounces: new Bounces { Total = 8 }, clamp: new Clamp { Indirect = 10 })).Text();

        var parameters = _bridge.LastParameters!;

        Assert.Equal(BridgeCommands.SetCycles, _bridge.Sent.Single().Command);
        Assert.True(parameters["activate"]!.GetValue<bool>());
        Assert.Equal(8, parameters["bounces"]!["total"]!.GetValue<int>());
        Assert.Equal(10, parameters["clamp"]!["indirect"]!.GetValue<int>());
    }

    [Fact]
    public async Task Freestyle_Linesets_TravelAsSnakeCaseBlocks()
    {
        var tools = new EngineTools(_bridge);

        (await tools.FreestyleAsync(enabled: true, linesets: [new Lineset { Name = "Ink", Edges = ["silhouette"], Thickness = 3 }])).Text();

        Assert.Equal(BridgeCommands.SetFreestyle, _bridge.Sent.Single().Command);
        Assert.Equal("silhouette", _bridge.LastParameters!["linesets"]![0]!["edges"]![0]!.ToString());
    }

    [Fact]
    public async Task ViewLayer_CollectionsAndLightGroups_AreForwarded()
    {
        var tools = new LayerTools(_bridge);

        (await tools.ViewLayerAsync(name: "Fg", collections: new LayerCollections { Exclude = ["Background"] }, lightGroups: ["Key"], lightGroupMembers: new JsonObject { ["Key"] = new JsonArray("Sun") })).Text();

        var parameters = _bridge.LastParameters!;

        Assert.Equal("Background", parameters["collections"]!["exclude"]![0]!.ToString());
        Assert.Equal("Key", parameters["light_groups"]![0]!.ToString());
        Assert.Equal("Sun", parameters["light_group_members"]!["Key"]![0]!.ToString());
    }

    [Fact]
    public async Task LightLinking_DefaultsToInclude()
    {
        var tools = new LayerTools(_bridge);

        (await tools.LightLinkingAsync("Key", receivers: ["Hero"])).Text();

        Assert.Equal("include", _bridge.LastParameters!["receiver_mode"]!.ToString());
        Assert.Equal("Hero", _bridge.LastParameters["receivers"]![0]!.ToString());
    }

    [Fact]
    public async Task CameraOptics_ApertureFocusAndShutterCurve_AreForwarded()
    {
        var tools = new OpticsTools(_bridge);

        (await tools.OpticsAsync(lens: 35, aperture: new Aperture { Fstop = 2.8, Blades = 7 }, focus: new Focus { Object = "Face" })).Text();

        var parameters = _bridge.LastParameters!;

        Assert.Equal(BridgeCommands.SetCameraOptics, _bridge.Sent.Single().Command);
        Assert.Equal(7, parameters["aperture"]!["blades"]!.GetValue<int>());
        Assert.Equal("Face", parameters["focus"]!["object"]!.ToString());
        Assert.False(parameters.ContainsKey("shutter_curve"));
    }

    [Fact]
    public async Task CameraMoveAndLightRig_SendWireNames()
    {
        var tools = new StagingTools(_bridge);

        (await tools.CameraMoveAsync("orbit", target: "Hero", startAngle: 30, distanceEnd: 2)).Text();
        (await tools.LightRigAsync(target: "Hero", keyEnergy: 800, fillRatio: 0.3, keyTemperature: 5600)).Text();

        Assert.Equal(BridgeCommands.CameraMove, _bridge.Sent[0].Command);
        Assert.Equal(30, _bridge.Sent[0].Parameters!["start_angle"]!.GetValue<double>());
        Assert.Equal(BridgeCommands.LightRig, _bridge.Sent[1].Command);
        Assert.Equal(5600, _bridge.Sent[1].Parameters!["key_temperature"]!.GetValue<double>());
    }

    [Fact]
    public async Task ObjectRenderFlags_VisibilityAndPassIndex_AreForwarded()
    {
        var tools = new RenderFlagTools(_bridge);

        (await tools.ObjectRenderFlagsAsync(["Hero", "Props"], holdout: true, visible: new RayVisibility { Glossy = false }, passIndex: 3)).Text();

        var parameters = _bridge.LastParameters!;

        Assert.Equal(BridgeCommands.ObjectRenderFlags, _bridge.Sent.Single().Command);
        Assert.Equal("Props", parameters["names"]![1]!.ToString());
        Assert.False(parameters["visible"]!["glossy"]!.GetValue<bool>());
        Assert.Equal(3, parameters["pass_index"]!.GetValue<int>());
    }

    [Fact]
    public async Task RenderObject_ResolutionAboveTheLimit_IsRefused()
    {
        var tools = new ShotTools(_bridge, new RenderWatch(new SilentMonitor()), new RenderGallery());

        var result = await tools.RenderObjectAsync("/tmp/shot.png", "Cube", resolutionX: 5000);
        var response = JsonNode.Parse(((TextContentBlock)result.Content[0]).Text)!;

        Assert.False(response["ok"]!.GetValue<bool>());
        Assert.Empty(_bridge.Sent);
    }

    [Fact]
    public async Task RenderObject_DefaultTimeout_IsARenderTimeout()
    {
        var tools = new ShotTools(_bridge, new RenderWatch(new SilentMonitor()), new RenderGallery());

        await tools.RenderObjectAsync("/tmp/shot.png", names: ["A", "B"], azimuth: 90);

        Assert.Equal(TimeSpan.FromSeconds(300), _bridge.Sent.Single().Timeout);
        Assert.Equal("B", _bridge.LastParameters!["names"]![1]!.ToString());
        Assert.False(_bridge.LastParameters.ContainsKey("name"));
    }
}
