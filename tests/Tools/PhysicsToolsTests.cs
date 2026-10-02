using Snail.MCP.Blender.Tools.Physics;

namespace Snail.MCP.Blender.Tests.Tools;

public class PhysicsToolsTests
{
    private readonly ScriptedBridge _bridge = new();

    [Fact]
    public async Task Particles_Hair_SendsLengthUnderTheWireName()
    {
        var tools = new PhysicsTools(_bridge);

        (await tools.ParticlesAsync("Head", "HAIR", count: 2000, hairLength: 0.3)).Text();

        Assert.Equal(0.3, _bridge.LastParameters!["hair_length"]!.GetValue<double>());
        Assert.Equal(2000, _bridge.LastParameters["count"]!.GetValue<int>());
    }
}
