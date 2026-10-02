using Snail.MCP.Blender.Application.Rendering;
using Snail.MCP.Blender.Tools.Video;

namespace Snail.MCP.Blender.Tests.Tools;

public class VideoToolsTests
{
    private readonly ScriptedBridge _bridge = new();

    private SequencerTools Timeline() => new(_bridge, new RenderWatch(new SilentMonitor()));

    [Fact]
    public async Task AddStrip_TextWithFade_SendsWireNames()
    {
        var tools = Timeline();

        (await tools.AddStripAsync("text", text: "Hello", fontSize: 80, length: 48, fade: new StripFade { In = 12 }, transform: new StripTransform { Crop = [2, 2, 0, 0], Flip = [true, false] })).Text();

        var parameters = _bridge.LastParameters!;

        Assert.Equal(BridgeCommands.SequencerAddStrip, _bridge.Sent.Single().Command);
        Assert.Equal(80, parameters["font_size"]!.GetValue<double>());
        Assert.Equal(12, parameters["fade"]!["in"]!.GetValue<int>());
        Assert.Equal(2, parameters["transform"]!["crop"]![1]!.GetValue<int>());
        Assert.True(parameters["transform"]!["flip"]![0]!.GetValue<bool>());
        Assert.Null(parameters["transform"]!["rotation"]);
        Assert.True(parameters["with_sound"]!.GetValue<bool>());
        Assert.Equal(TimeSpan.FromSeconds(120), _bridge.Sent.Single().Timeout);
    }

    [Fact]
    public async Task Render_ResolutionAboveTheLimit_IsRefused()
    {
        var tools = Timeline();

        var response = JsonNode.Parse((await tools.RenderAsync("/tmp/out.mp4", resolutionX: 8000)).Text())!;

        Assert.False(response["ok"]!.GetValue<bool>());
        Assert.Empty(_bridge.Sent);
    }

    [Fact]
    public async Task AddEffect_TwoInputs_AreForwarded()
    {
        var tools = Timeline();

        (await tools.AddEffectAsync("CROSS", ["A", "B"], length: 12)).Text();

        Assert.Equal(["A", "B"], _bridge.LastParameters!["inputs"]!.AsArray().Select(node => node!.ToString()));
        Assert.Equal(12, _bridge.LastParameters["length"]!.GetValue<int>());
    }

    [Fact]
    public async Task SequencerProxy_SizesBecomeAnArray_AndTheTimeoutFitsABuild()
    {
        var tools = new EditTools(_bridge);

        (await tools.ProxyAsync(names: ["Clip"], sizes: [25, 50])).Text();

        Assert.Equal([25, 50], _bridge.LastParameters!["sizes"]!.AsArray().Select(node => node!.GetValue<int>()));
        Assert.Equal(TimeSpan.FromSeconds(600), _bridge.Sent.Single().Timeout);
    }

    [Fact]
    public async Task SequencerTiming_ResolutionAboveTheLimit_IsRefused()
    {
        var tools = new EditTools(_bridge);

        var response = JsonNode.Parse((await tools.TimingAsync(fps: 23.976, resolutionY: 5000)).Text())!;

        Assert.False(response["ok"]!.GetValue<bool>());
        Assert.Empty(_bridge.Sent);
    }

    [Fact]
    public async Task SequencerEncode_ProresAndAudio_AreForwarded()
    {
        var tools = new DeliveryTools(_bridge);

        (await tools.EncodeAsync(container: "mov", proresProfile: "422_HQ", audio: new EncodeAudio { Codec = "PCM", SampleRate = 48000 })).Text();

        var parameters = _bridge.LastParameters!;

        Assert.Equal(BridgeCommands.SequencerEncode, _bridge.Sent.Single().Command);
        Assert.Equal("422_HQ", parameters["prores_profile"]!.ToString());
        Assert.Equal(48000, parameters["audio"]!["sample_rate"]!.GetValue<int>());
    }
}
