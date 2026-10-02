using Snail.MCP.Blender.Tools.Io;

namespace Snail.MCP.Blender.Tests.Tools;

public class IoToolsTests
{
    private readonly ScriptedBridge _bridge = new();

    [Fact]
    public async Task Export_SelectedNames_AreForwardedWithTheFlag()
    {
        var tools = new IoTools(_bridge);

        (await tools.ExportAsync("/tmp/scene.glb", selectedOnly: true, names: ["Cube"])).Text();

        var parameters = _bridge.LastParameters!;

        Assert.True(parameters["selected_only"]!.GetValue<bool>());
        Assert.Equal("Cube", parameters["names"]![0]!.ToString());
        Assert.Equal(TimeSpan.FromSeconds(120), _bridge.Sent.Single().Timeout);
    }
}
