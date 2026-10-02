using Snail.MCP.Blender.Tools.Post;

namespace Snail.MCP.Blender.Tests.Tools;

public class PostToolsTests
{
    private readonly ScriptedBridge _bridge = new();

    [Fact]
    public async Task LensEffects_BlocksAndClear_UseWireNames()
    {
        var tools = new PostTools(_bridge);

        (await tools.EffectsAsync(glare: new Glare { Type = "STREAKS" }, chromaticAberration: new ChromaticAberration { Dispersion = 0.03 }, vectorBlur: new VectorBlur { Samples = 16 }, grain: new Grain { Strength = 0.1, Response = new GrainResponse { Highlights = 0.1 } })).Text();

        var parameters = _bridge.LastParameters!;

        Assert.Equal(BridgeCommands.LensEffects, _bridge.Sent.Single().Command);
        Assert.Equal(0.03, parameters["chromatic_aberration"]!["dispersion"]!.GetValue<double>());
        Assert.Equal(16, parameters["vector_blur"]!["samples"]!.GetValue<int>());
        Assert.Equal(0.1, parameters["grain"]!["response"]!["highlights"]!.GetValue<double>());
        Assert.False(parameters["clear"]!.GetValue<bool>());
    }

    [Fact]
    public async Task Compositor_RemoveNode_SendsTheNameAsNode()
    {
        var tools = new CompositorTools(_bridge);

        (await tools.CompositorAsync("remove_node", name: "Blur")).Text();

        Assert.Equal("Blur", _bridge.LastParameters!["node"]!.ToString());
    }

    [Fact]
    public async Task FileOutput_SlotsAndAllPasses_AreForwarded()
    {
        var tools = new CompositorTools(_bridge);

        (await tools.FileOutputAsync(directory: "/renders/passes", fileFormat: "OPEN_EXR_MULTILAYER", slots: new JsonArray("Image", new JsonObject { ["pass"] = "Depth", ["name"] = "Z" }))).Text();

        var parameters = _bridge.LastParameters!;

        Assert.Equal("configure", parameters["action"]!.ToString());
        Assert.Equal("Z", parameters["slots"]![1]!["name"]!.ToString());
        Assert.False(parameters["all_passes"]!.GetValue<bool>());
    }
}
