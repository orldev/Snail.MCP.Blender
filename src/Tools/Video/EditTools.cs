using System.ComponentModel;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Snail.MCP.Blender.Application.Skills;
using Snail.MCP.Blender.Tools.Prompts;

namespace Snail.MCP.Blender.Tools.Video;

/// <summary>The cut itself: splitting strips, proxies for playback, project timing and grading.</summary>
[McpServerToolType]
[Skill(Skills.Video, SkillDescriptions.Video)]
public sealed class EditTools(IBlenderBridge bridge) : BridgeToolBase(bridge)
{
    [McpServerTool(Name = "blender_sequencer_split", Destructive = false, OpenWorld = false)]
    [Description(ToolDescriptions.Cuts.Split)]
    public Task<CallToolResult> SplitAsync(
        [Description("Strip to cut, as blender_sequencer_info lists it.")] string name,
        [Description("Frame where the cut goes; the right part starts on it.")] int frame,
        [Description("both (the default), left or right: which parts stay.")] string keep = "both",
        [Description("Name for the right part; the strip's name plus .R otherwise.")] string? rightName = null,
        CancellationToken cancellationToken = default) =>
        SendAsync(BridgeCommands.SequencerSplit, new JsonObject().With("name", name).With("frame", frame).With("keep", keep).With("right_name", rightName), cancellationToken);

    [McpServerTool(Name = "blender_sequencer_proxy", Destructive = false, OpenWorld = false)]
    [Description(ToolDescriptions.Cuts.Proxy)]
    public Task<CallToolResult> ProxyAsync(
        [Description("Strips to set up; every movie and image strip with all=true.")] string[]? names = null,
        [Description("Every strip instead of a list.")] bool all = false,
        [Description("Proxy sizes in percent: 25, 50, 75, 100; [25] is the default and enough for editing.")] int[]? sizes = null,
        [Description("JPEG quality of the proxies, 1 to 100.")] int? quality = null,
        [Description("Project folder for the proxies instead of next to each source.")] string? directory = null,
        [Description("Rebuild proxies that exist.")] bool overwrite = false,
        [Description("Build the proxies now; needs the Video Sequencer editor open in Blender, otherwise only the settings are stored.")] bool build = true,
        [Description("Use proxies on these strips, or stop using them.")] bool enabled = true,
        CancellationToken cancellationToken = default) =>
        SendAsync(BridgeCommands.SequencerProxy,
            new JsonObject().With("names", names).With("all", all).With("sizes", sizes is null ? null : new JsonArray([.. sizes.Select(size => (JsonNode)size)])).With("quality", quality)
                .With("directory", directory).With("overwrite", overwrite).With("build", build).With("enabled", enabled),
            cancellationToken, TimeSpan.FromSeconds(600));

    [McpServerTool(Name = "blender_sequencer_timing", Destructive = false, OpenWorld = false)]
    [Description(ToolDescriptions.Cuts.Timing)]
    public Task<CallToolResult> TimingAsync(
        [Description("Frames per second; 23.976, 29.97 and 59.94 become the matching fractional rates.")] double? fps = null,
        [Description("NONE, FRAME_DROP (skip frames to keep time) or AUDIO_SYNC (follow the audio clock) for playback.")] string? syncMode = null,
        [Description("Pixel aspect [x, y]; [1, 1] for square pixels.")] double[]? pixelAspect = null,
        [Description(ToolDescriptions.Parameters.RenderResolution)] int? resolutionX = null,
        [Description(ToolDescriptions.Parameters.RenderResolution)] int? resolutionY = null,
        [Description("Resolution percentage 1 to 100.")] int? percentage = null,
        [Description("First frame of the timeline.")] int? start = null,
        [Description("Last frame of the timeline.")] int? end = null,
        [Description("Play audio during playback.")] bool? audio = null,
        CancellationToken cancellationToken = default) =>
        ToolLimits.IsRenderable(resolutionX) && ToolLimits.IsRenderable(resolutionY)
            ? SendAsync(BridgeCommands.SequencerTiming,
                new JsonObject().With("fps", fps).With("sync_mode", syncMode).With("pixel_aspect", pixelAspect).With("resolution_x", resolutionX).With("resolution_y", resolutionY)
                    .With("percentage", percentage).With("start", start).With("end", end).With("audio", audio),
                cancellationToken)
            : Task.FromResult(ToolResponse.Failure(Messages.ResolutionTooLarge, Messages.ResolutionHint));

    [McpServerTool(Name = "blender_sequencer_grade", Destructive = false, OpenWorld = false)]
    [Description(ToolDescriptions.Cuts.Grade)]
    public Task<CallToolResult> GradeAsync(
        [Description("Strip to grade.")] string name,
        [Description(ToolDescriptions.Parameters.StripModifier)] string? type = null,
        [Description("Modifier name to update, or to give a new one; the type's name otherwise.")] string? modifier = null,
        [Description("COLOR_BALANCE primary grade; 1 is neutral.")] ColorBalance? colorBalance = null,
        [Description("CURVES: points per channel {\"C\": [[0, 0], [0.25, 0.2], [0.75, 0.8], [1, 1]], \"R\": [...]}; HUE_CORRECT uses H, S, V.")] JsonObject? curves = null,
        [Description("Other modifier settings by Python name: BRIGHT_CONTRAST {\"bright\": 5, \"contrast\": 10}, WHITE_BALANCE {\"white_value\": [r, g, b]}, TONEMAP {\"tonemap_type\": \"RD_PHOTORECEPTOR\", \"intensity\": 0}.")] JsonObject? settings = null,
        [Description("Strip whose picture masks the modifier.")] string? maskStrip = null,
        [Description("Mute or unmute the modifier.")] bool? mute = null,
        [Description("Modifier name to remove.")] string? remove = null,
        [Description("Remove every modifier from the strip.")] bool clear = false,
        CancellationToken cancellationToken = default) =>
        SendAsync(BridgeCommands.SequencerGrade,
            new JsonObject().With("name", name).With("type", type).With("modifier", modifier).With("color_balance", colorBalance).With("curves", curves).With("settings", settings)
                .With("mask_strip", maskStrip).With("mute", mute).With("remove", remove).With("clear", clear),
            cancellationToken);

    [McpServerTool(Name = "blender_sequencer_meta", Destructive = false, OpenWorld = false)]
    [Description(ToolDescriptions.Cuts.Meta)]
    public Task<CallToolResult> MetaAsync(
        [Description("make (the default) groups the named strips; separate unpacks the named meta strip.")] string action = "make",
        [Description("Strips to group, or the meta strip to separate.")] string[]? names = null,
        [Description("make: name for the meta strip.")] string? newName = null,
        CancellationToken cancellationToken = default) =>
        SendAsync(BridgeCommands.SequencerMeta, new JsonObject().With("action", action).With("names", names).With("new_name", newName), cancellationToken);
}
