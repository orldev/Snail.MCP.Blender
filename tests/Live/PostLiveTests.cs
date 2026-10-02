using Snail.MCP.Blender.Adapters.Blender;

namespace Snail.MCP.Blender.Tests.Live;

/// <summary>The post skill against a real Blender: a compositor node, File Output of passes, a cryptomatte matte and the lens-effects stack.</summary>
public sealed class PostLiveTests : IAsyncLifetime
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
    public async Task Compositor_BlurNodeAddedByHand_RendersThroughTheNodeGroup()
    {
        await Send(BridgeCommands.AddPrimitive, new JsonObject { ["kind"] = "monkey", ["name"] = "Subject" });
        await Send(BridgeCommands.AddCamera, new JsonObject { ["location"] = new JsonArray(0, -5, 2), ["look_at"] = new JsonArray(0, 0, 0) });
        await Send(BridgeCommands.RenderSettings, new JsonObject { ["engine"] = "EEVEE", ["resolution_x"] = 64, ["resolution_y"] = 64, ["samples"] = 1, ["filter_size"] = 1.0 });

        var cleared = await Send(BridgeCommands.Compositor, new JsonObject { ["action"] = "clear" });
        Assert.Single(cleared["links"]!.AsArray());

        var blur = await Send(BridgeCommands.Compositor, new JsonObject { ["action"] = "add_node", ["type"] = "Blur", ["name"] = "Soft", ["inputs"] = new JsonObject { ["Size"] = new JsonArray(4, 4) } });
        Assert.Equal("CompositorNodeBlur", blur["type"]!.ToString());
        await Send(BridgeCommands.Compositor, new JsonObject { ["action"] = "link", ["from_node"] = "Render Layers", ["from_socket"] = "Image", ["to_node"] = "Soft", ["to_socket"] = "Image" });
        var linked = await Send(BridgeCommands.Compositor, new JsonObject { ["action"] = "link", ["from_node"] = "Soft", ["from_socket"] = "Image", ["to_node"] = "Group Output", ["to_socket"] = "Image" });
        Assert.True(linked["links"]!.GetValue<int>() >= 2);

        var moved = await _link.SendAsync(BridgeCommands.Compositor, new JsonObject { ["action"] = "grain" });
        Assert.Equal("BadRequest", moved.Error!.Type);

        var path = Path.Combine(_workDirectory, "composited.png");
        var rendered = await Send(BridgeCommands.RenderImage, new JsonObject { ["path"] = path }, TimeSpan.FromSeconds(180));
        Assert.True(File.Exists(rendered["path"]!.ToString()));
    }

    [BlenderFact]
    public async Task FileOutput_MultilayerExrOfPasses_IsWrittenByARender()
    {
        await Send(BridgeCommands.RenderSettings, new JsonObject { ["engine"] = "EEVEE", ["resolution_x"] = 48, ["resolution_y"] = 48, ["samples"] = 1 });
        var passes = Path.Combine(_workDirectory, "passes");

        var node = await Send(BridgeCommands.FileOutput, new JsonObject { ["directory"] = passes, ["file_name"] = "shot_", ["file_format"] = "OPEN_EXR_MULTILAYER", ["color_depth"] = 32, ["slots"] = new JsonArray("Image", "depth", new JsonObject { ["pass"] = "Normal", ["name"] = "N" }) });

        Assert.Equal(["Image", "Depth", "N"], node["slots"]!.AsArray().Select(slot => slot!["name"]!.ToString()));
        Assert.Equal("Render Layers.Normal", node["slots"]![2]!["from"]!.ToString());

        var rendered = await Send(BridgeCommands.RenderImage, new JsonObject { ["path"] = Path.Combine(_workDirectory, "beauty.png") }, TimeSpan.FromSeconds(120));

        Assert.True(rendered["duration_ms"]!.GetValue<int>() > 0);
        Assert.Single(Directory.EnumerateFiles(passes, "shot_*.exr"));
    }

    [BlenderFact]
    public async Task CryptomatteMatte_ForAnObject_WritesThroughAFileOutputSlot()
    {
        await Send(BridgeCommands.RenderSettings, new JsonObject { ["engine"] = "EEVEE", ["resolution_x"] = 32, ["resolution_y"] = 32, ["samples"] = 1 });
        var mattes = Path.Combine(_workDirectory, "mattes");

        var matte = await Send(BridgeCommands.CryptomatteMatte, new JsonObject { ["names"] = new JsonArray("Cube"), ["output"] = "Mattes", ["directory"] = mattes });

        Assert.Equal("Cube", matte["matte_id"]!.ToString());
        Assert.Equal("ViewLayer.CryptoObject", matte["layer"]!.ToString());
        Assert.Contains("Matte", matte["outputs"]!.AsArray().Select(node => node!.ToString()));

        await Send(BridgeCommands.RenderImage, new JsonObject { ["path"] = Path.Combine(_workDirectory, "beauty.png"), ["preview"] = false }, TimeSpan.FromSeconds(120));

        Assert.NotEmpty(Directory.EnumerateFiles(mattes, "*.exr"));
    }

    [BlenderFact]
    public async Task LensEffects_FullStack_RendersThroughTheChain()
    {
        await Send(BridgeCommands.SetCycles, new JsonObject { ["samples"] = 4 });
        await Send(BridgeCommands.RenderSettings, new JsonObject { ["resolution_x"] = 48, ["resolution_y"] = 48 });

        var stack = await Send(BridgeCommands.LensEffects, new JsonObject
        {
            ["denoise"] = new JsonObject { ["use_data_passes"] = true },
            ["defocus"] = new JsonObject { ["fstop"] = 2.8 },
            ["glare"] = new JsonObject { ["type"] = "STREAKS", ["threshold"] = 1.5 },
            ["halation"] = new JsonObject { ["strength"] = 0.2 },
            ["chromatic_aberration"] = new JsonObject { ["dispersion"] = 0.03 },
            ["vignette"] = new JsonObject { ["amount"] = 0.5 },
            ["grain"] = new JsonObject { ["strength"] = 0.1, ["size"] = 1.5 },
        });

        Assert.Equal(["denoise", "defocus", "glare", "halation", "chromatic_aberration", "vignette", "grain:to_display", "grain", "grain:to_linear"], stack["chain"]!.AsArray().Select(node => node!.ToString()));
        Assert.Contains("denoising_data", stack["passes_enabled"]!.AsArray().Select(node => node!.ToString()));

        var rendered = await Send(BridgeCommands.RenderImage, new JsonObject { ["path"] = Path.Combine(_workDirectory, "fx.png") }, TimeSpan.FromSeconds(180));
        Assert.True(File.Exists(rendered["path"]!.ToString()));

        var cleared = await Send(BridgeCommands.LensEffects, new JsonObject { ["clear"] = true });
        Assert.True(cleared["cleared"]!.GetValue<bool>());
    }

    /// <summary>A call that names no effect is refused, and the compositor is left exactly as it was.</summary>
    /// <remarks>It used to take the stack down and answer cleared: true, so a call made to see what was in the compositor emptied it, and a
    /// chain wired by hand could not be put back — there was nothing left to read it from. The node added here is not one of the stack's own
    /// (those are named FX: and a rebuild removes them on purpose); it stands for the work of a person, and the refusal must not cost it.</remarks>
    [BlenderFact]
    public async Task LensEffects_AskedForNothing_IsRefused_AndLeavesTheCompositorAlone()
    {
        await Send(BridgeCommands.Compositor, new JsonObject { ["action"] = "add_node", ["type"] = "CompositorNodeBlur", ["name"] = "ByHand" });

        var refused = await _link.SendAsync(BridgeCommands.LensEffects, []);
        var after = await Send(BridgeCommands.Compositor, new JsonObject { ["action"] = "info" });

        Assert.Equal("BadRequest", refused.Error?.Type);
        Assert.Contains("clear", refused.Error!.Message, StringComparison.Ordinal);
        Assert.Contains("ByHand", after["nodes"]!.AsArray().Select(node => node!["name"]!.ToString()));
    }

    private async Task<JsonNode> Send(BridgeCommand command, JsonObject? parameters = null, TimeSpan? timeout = null)
    {
        var reply = await _link.SendAsync(command, parameters, timeout);

        Assert.True(reply.IsOk, $"{command.Name}: {reply.Error?.Type} {reply.Error?.Message} {reply.Error?.Details}");

        return reply.Result!;
    }
}
