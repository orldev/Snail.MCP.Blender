using System.ComponentModel;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Snail.MCP.Blender.Application.Rendering;
using Snail.MCP.Blender.Application.Skills;
using Snail.MCP.Blender.Tools.Prompts;

namespace Snail.MCP.Blender.Tools.Video;

/// <summary>The Video Sequence Editor: a timeline of strips encoded to a video file.</summary>
[McpServerToolType]
[Skill(Skills.Video, SkillDescriptions.Video)]
public sealed class SequencerTools(IBlenderBridge bridge, RenderWatch watch) : BridgeToolBase(bridge)
{
    [McpServerTool(Name = "blender_sequencer_info", ReadOnly = true, OpenWorld = false)]
    [Description(ToolDescriptions.Video.Info)]
    public Task<CallToolResult> InfoAsync(CancellationToken cancellationToken = default) =>
        SendAsync(BridgeCommands.SequencerInfo, [], cancellationToken);

    [McpServerTool(Name = "blender_sequencer_add_strip", Destructive = false, OpenWorld = false)]
    [Description(ToolDescriptions.Video.AddStrip)]
    public Task<CallToolResult> AddStripAsync(
        [Description(ToolDescriptions.Parameters.StripKind)] string type,
        [Description("Name for the strip.")] string? name = null,
        [Description("movie, image, sound: absolute path of the file.")] string? path = null,
        [Description("scene: name of the scene to render into the strip; the current scene otherwise.")] string? scene = null,
        [Description("Timeline channel (row), 1 and up; the first free one otherwise.")] int? channel = null,
        [Description("Frame the strip starts on; the scene start otherwise.")] int? frameStart = null,
        [Description("color, text, image: length in frames.")] int? length = null,
        [Description("text: the text to show.")] string? text = null,
        [Description("text: font size in pixels.")] double? fontSize = null,
        [Description("color: [r, g, b]; text: [r, g, b] or [r, g, b, a].")] double[]? color = null,
        [Description("text: [x, y] position, 0 to 1 from the bottom-left.")] double[]? location = null,
        [Description("movie: also add its audio as a sound strip on the next channel.")] bool withSound = true,
        [Description(ToolDescriptions.Parameters.StripBlend)] string? blendType = null,
        [Description(ToolDescriptions.Parameters.StripOpacity)] double? opacity = null,
        [Description(ToolDescriptions.Parameters.StripTransform)] StripTransform? transform = null,
        [Description(ToolDescriptions.Parameters.StripFade)] StripFade? fade = null,
        [Description(ToolDescriptions.Parameters.StripSettings)] JsonObject? settings = null,
        CancellationToken cancellationToken = default) =>
        SendAsync(BridgeCommands.SequencerAddStrip,
            new JsonObject().With("type", type).With("name", name).With("path", path).With("scene", scene).With("channel", channel).With("frame_start", frameStart)
                .With("length", length).With("text", text).With("font_size", fontSize).With("color", color).With("location", location).With("with_sound", withSound)
                .With("blend_type", blendType).With("opacity", opacity).With("transform", transform).With("fade", fade).With("settings", settings),
            cancellationToken, TimeSpan.FromSeconds(120));

    [McpServerTool(Name = "blender_sequencer_add_effect", Destructive = false, OpenWorld = false)]
    [Description(ToolDescriptions.Video.AddEffect)]
    public Task<CallToolResult> AddEffectAsync(
        [Description(ToolDescriptions.Parameters.EffectType)] string type,
        [Description("Input strip names: two for transitions and mixes, one for GLOW, GAUSSIAN_BLUR, SPEED and ADJUSTMENT, none for COLOR and TEXT.")] string[]? inputs = null,
        [Description("Name for the effect strip.")] string? name = null,
        [Description("Timeline channel; the first free one otherwise.")] int? channel = null,
        [Description("Start frame; where the inputs overlap otherwise.")] int? frameStart = null,
        [Description("Length in frames; the overlap of the inputs otherwise.")] int? length = null,
        [Description(ToolDescriptions.Parameters.StripSettings)] JsonObject? settings = null,
        CancellationToken cancellationToken = default) =>
        SendAsync(BridgeCommands.SequencerAddEffect,
            new JsonObject().With("type", type).With("inputs", inputs).With("name", name).With("channel", channel).With("frame_start", frameStart).With("length", length).With("settings", settings),
            cancellationToken);

    [McpServerTool(Name = "blender_sequencer_update_strip", Destructive = false, OpenWorld = false)]
    [Description(ToolDescriptions.Video.UpdateStrip)]
    public Task<CallToolResult> UpdateStripAsync(
        [Description("Strip name as blender_sequencer_info lists it.")] string name,
        [Description("Move to this channel.")] int? channel = null,
        [Description("Move the strip to start on this frame.")] int? frameStart = null,
        [Description("New length in frames (for images, colours, text and effects).")] int? length = null,
        [Description("Frames cut from the beginning of the source.")] int? trimStart = null,
        [Description("Frames cut from the end of the source.")] int? trimEnd = null,
        [Description("Silence or hide the strip.")] bool? mute = null,
        [Description(ToolDescriptions.Parameters.StripBlend)] string? blendType = null,
        [Description(ToolDescriptions.Parameters.StripOpacity)] double? opacity = null,
        [Description("sound: volume, 1 is the source level.")] double? volume = null,
        [Description("text: new text.")] string? text = null,
        [Description("color: new [r, g, b].")] double[]? color = null,
        [Description(ToolDescriptions.Parameters.StripTransform)] StripTransform? transform = null,
        [Description(ToolDescriptions.Parameters.StripFade)] StripFade? fade = null,
        [Description(ToolDescriptions.Parameters.StripSettings)] JsonObject? settings = null,
        [Description("Rename the strip.")] string? newName = null,
        CancellationToken cancellationToken = default) =>
        SendAsync(BridgeCommands.SequencerUpdateStrip,
            new JsonObject().With("name", name).With("channel", channel).With("frame_start", frameStart).With("length", length).With("trim_start", trimStart).With("trim_end", trimEnd)
                .With("mute", mute).With("blend_type", blendType).With("opacity", opacity).With("volume", volume).With("text", text).With("color", color)
                .With("transform", transform).With("fade", fade).With("settings", settings).With("new_name", newName),
            cancellationToken);

    [McpServerTool(Name = "blender_sequencer_remove_strip", Destructive = true, OpenWorld = false)]
    [Description(ToolDescriptions.Video.RemoveStrip)]
    public Task<CallToolResult> RemoveStripAsync(
        [Description("Strip to remove.")] string? name = null,
        [Description("Remove every strip and start over.")] bool all = false,
        CancellationToken cancellationToken = default) =>
        SendAsync(BridgeCommands.SequencerRemoveStrip, new JsonObject().With("name", name).With("all", all), cancellationToken);

    [McpServerTool(Name = "blender_sequencer_render", Destructive = false, OpenWorld = false)]
    [Description(ToolDescriptions.Video.Render)]
    public async Task<CallToolResult> RenderAsync(
        [Description("Absolute path of the video to write; the extension picks the container: .mp4, .mkv, .webm, .mov, .avi, .ogv.")] string path,
        [Description("H264 (the default), H265, AV1, WEBM (VP9), DNXHD, FFV1 and the rest FFmpeg offers.")] string? codec = null,
        [Description("LOSSLESS, PERC_LOSSLESS, HIGH, MEDIUM (the default), LOW, VERYLOW, LOWEST.")] string? quality = null,
        [Description("AAC (the default), MP3, VORBIS, OPUS, FLAC, PCM or NONE.")] string? audioCodec = null,
        [Description("Set the frame range to cover every strip before encoding.")] bool fitRange = true,
        [Description("First frame to encode; overrides fitRange.")] int? start = null,
        [Description("Last frame to encode; overrides fitRange.")] int? end = null,
        [Description("Frames per second.")] int? fps = null,
        [Description(ToolDescriptions.Parameters.RenderResolution)] int? resolutionX = null,
        [Description(ToolDescriptions.Parameters.RenderResolution)] int? resolutionY = null,
        [Description(ToolDescriptions.Parameters.TimeoutSeconds)] int timeoutSeconds = 600,
        IProgress<ProgressNotificationValue>? progress = null,
        CancellationToken cancellationToken = default) =>
        ToolLimits.IsRenderable(resolutionX) && ToolLimits.IsRenderable(resolutionY)
            ? await WithinAsync(timeoutSeconds, async timeout => ToolResponse.From(await watch.FollowAsync(
                ExchangeAsync(BridgeCommands.SequencerRender,
                    new JsonObject().With("path", path).With("codec", codec).With("quality", quality).With("audio_codec", audioCodec).With("fit_range", fitRange)
                        .With("start", start).With("end", end).With("fps", fps).With("resolution_x", resolutionX).With("resolution_y", resolutionY),
                    timeout, cancellationToken),
                progress, start is { } first && end is { } last ? last - first + 1 : null, cancellationToken).ConfigureAwait(false))).ConfigureAwait(false)
            : ToolResponse.Failure(Messages.ResolutionTooLarge, Messages.ResolutionHint);
}
