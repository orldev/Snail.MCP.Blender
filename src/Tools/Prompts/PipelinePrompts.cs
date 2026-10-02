using System.ComponentModel;
using ModelContextProtocol.Server;

namespace Snail.MCP.Blender.Tools.Prompts;

/// <summary>Recipes a studio follows, offered as MCP prompts so a client can start a task with the right order of tools already in front of the model.</summary>
[McpServerPromptType]
public sealed class PipelinePrompts
{
    [McpServerPrompt(Name = PromptTexts.ProductShotName)]
    [Description(PromptTexts.ProductShotDescription)]
    public string ProductShot(
        [Description("Object to shoot, as the outliner names it.")] string subject,
        [Description("Delivery: PNG for review, OPEN_EXR for compositing, JPEG for a quick send.")] string output = "PNG") =>
        PromptTexts.ProductShot.Replace("{subject}", subject, StringComparison.Ordinal).Replace("{output}", output, StringComparison.Ordinal);

    [McpServerPrompt(Name = PromptTexts.FarmDeliveryName)]
    [Description(PromptTexts.FarmDeliveryDescription)]
    public string FarmDelivery(
        [Description("Frames to render: 1-240, 1-240x2 or a list.")] string frames,
        [Description("Absolute folder for the EXR files.")] string directory) =>
        PromptTexts.FarmDelivery.Replace("{frames}", frames, StringComparison.Ordinal).Replace("{directory}", directory, StringComparison.Ordinal);

    [McpServerPrompt(Name = PromptTexts.VideoDeliveryName)]
    [Description(PromptTexts.VideoDeliveryDescription)]
    public string VideoDelivery(
        [Description("Source files or folders, comma separated.")] string sources,
        [Description("Delivery target: ProRes master, H264 review, WebM.")] string delivery = "H264 review") =>
        PromptTexts.VideoDelivery.Replace("{sources}", sources, StringComparison.Ordinal).Replace("{delivery}", delivery, StringComparison.Ordinal);
}
