using Snail.MCP.Blender.Tools.Materials;

namespace Snail.MCP.Blender.Tests.Tools;

public class MaterialsToolsTests
{
    private readonly ScriptedBridge _bridge = new();

    [Fact]
    public async Task CreateMaterial_PrincipledValues_UseTheAddOnKeys()
    {
        var tools = new MaterialTools(_bridge);

        (await tools.CreateAsync("Gold", assignTo: "Ring", baseColor: [1, 0.8, 0.2], metallic: 1, roughness: 0.3)).Text();

        var parameters = _bridge.LastParameters!;

        Assert.Equal(BridgeCommands.CreateMaterial, _bridge.Sent.Single().Command);
        Assert.Equal(["name", "assign_to", "base_color", "metallic", "roughness"], parameters.Select(pair => pair.Key));
        Assert.Equal(0.2, parameters["base_color"]![2]!.GetValue<double>());
    }

    [Fact]
    public async Task ProceduralTexture_EmptyRamp_AsksForTheDefaultRamp()
    {
        var tools = new TextureTools(_bridge);

        (await tools.ProceduralAsync("Rock", "voronoi", colorRamp: [], connectTo: "Normal", bumpStrength: 0.4)).Text();

        var parameters = _bridge.LastParameters!;

        Assert.True(parameters["color_ramp"]!.GetValue<bool>());
        Assert.Equal("Normal", parameters["connect_to"]!.ToString());
        Assert.Equal(0.4, parameters["bump_strength"]!.GetValue<double>());
    }

    [Fact]
    public async Task ProceduralTexture_RampStops_AreForwarded()
    {
        var tools = new TextureTools(_bridge);
        var stops = new JsonArray(new JsonObject { ["position"] = 0, ["color"] = new JsonArray(0, 0, 0) });

        (await tools.ProceduralAsync("Rock", colorRamp: stops)).Text();

        Assert.Equal(0, _bridge.LastParameters!["color_ramp"]![0]!["position"]!.GetValue<int>());
    }

    [Fact]
    public async Task BuildNodeGraph_SpecArrays_AreForwardedWithClearFlag()
    {
        var tools = new NodeTools(_bridge);
        var nodes = new JsonArray(new JsonObject { ["id"] = "noise", ["type"] = "TexNoise" });
        var links = new JsonArray(new JsonArray("noise", "Fac", "Principled BSDF", "Roughness"));

        (await tools.BuildAsync("Rock", nodes, links, clear: false)).Text();

        var parameters = _bridge.LastParameters!;

        Assert.Equal("TexNoise", parameters["nodes"]![0]!["type"]!.ToString());
        Assert.Equal("Roughness", parameters["links"]![0]![3]!.ToString());
        Assert.False(parameters["clear"]!.GetValue<bool>());
    }
}
