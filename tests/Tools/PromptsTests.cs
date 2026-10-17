using Snail.MCP.Blender.Tools.Prompts;

namespace Snail.MCP.Blender.Tests.Tools;

/// <summary>The studio recipes a client can start from: each one comes back with the caller's subject in it and no placeholder left unfilled.</summary>
public class PromptsTests
{
    private readonly PipelinePrompts _prompts = new();

    [Fact]
    public void ProductShot_CarriesTheSubjectAndTheDelivery()
    {
        var recipe = _prompts.ProductShot("Teapot", "OPEN_EXR");

        Assert.Contains("Teapot", recipe, StringComparison.Ordinal);
        Assert.Contains("OPEN_EXR", recipe, StringComparison.Ordinal);
        Assert.DoesNotContain("{subject}", recipe, StringComparison.Ordinal);
        Assert.DoesNotContain("{output}", recipe, StringComparison.Ordinal);
    }

    [Fact]
    public void FarmDelivery_CarriesTheRangeAndTheFolder()
    {
        var recipe = _prompts.FarmDelivery("1-240x2", "/renders/shot");

        Assert.Contains("1-240x2", recipe, StringComparison.Ordinal);
        Assert.Contains("/renders/shot", recipe, StringComparison.Ordinal);
        Assert.DoesNotContain("{frames}", recipe, StringComparison.Ordinal);
        Assert.DoesNotContain("{directory}", recipe, StringComparison.Ordinal);
    }

    [Fact]
    public void VideoDelivery_CarriesTheSourcesAndTheTarget()
    {
        var recipe = _prompts.VideoDelivery("/footage/a.mov, /footage/b.mov");

        Assert.Contains("/footage/b.mov", recipe, StringComparison.Ordinal);
        Assert.Contains("H264 review", recipe, StringComparison.Ordinal);
        Assert.DoesNotContain("{sources}", recipe, StringComparison.Ordinal);
        Assert.DoesNotContain("{delivery}", recipe, StringComparison.Ordinal);
    }
}
