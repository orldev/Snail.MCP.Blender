using System.ComponentModel;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Snail.MCP.Blender.Application.Rendering;
using Snail.MCP.Blender.Application.Skills;
using Snail.MCP.Blender.Configuration;
using Snail.MCP.Blender.Tools.Prompts;

namespace Snail.MCP.Blender.Tools.Rendering;

/// <summary>Render settings, renders to disk and render passes.</summary>
[McpServerToolType]
[Skill(Skills.Rendering, SkillDescriptions.Rendering)]
public sealed class RenderTools(IBlenderBridge bridge, RenderWatch watch, RenderGallery gallery, ServerConfig config, RenderJobs jobs) : BridgeToolBase(bridge)
{
    [McpServerTool(Name = "blender_render_settings", Destructive = false, OpenWorld = false)]
    [Description(ToolDescriptions.Rendering.Settings)]
    public Task<CallToolResult> SettingsAsync(
        [Description("EEVEE, CYCLES or WORKBENCH.")] string? engine = null,
        [Description(ToolDescriptions.Parameters.RenderResolution)] int? resolutionX = null,
        [Description(ToolDescriptions.Parameters.RenderResolution)] int? resolutionY = null,
        [Description("Resolution percentage 1 to 100; 50 renders a quick preview at half size.")] int? percentage = null,
        [Description("Samples per pixel; Cycles 64 to 1024, EEVEE 16 to 256.")] int? samples = null,
        [Description("Cycles denoising.")] bool? denoise = null,
        [Description("Cycles device: CPU or GPU.")] string? device = null,
        [Description("Transparent background instead of the world.")] bool? filmTransparent = null,
        [Description("Pixel filter width in pixels: 1.5 is Blender's default, 1.0 or lower renders sharper and crisper, 2 or more softer.")] double? filterSize = null,
        [Description("Camera object to render from.")] string? camera = null,
        [Description(ToolDescriptions.Parameters.Scene)] string? scene = null,
        CancellationToken cancellationToken = default) =>
        ToolLimits.IsRenderable(resolutionX) && ToolLimits.IsRenderable(resolutionY)
            ? SendAsync(BridgeCommands.RenderSettings,
                new JsonObject().With("engine", engine).With("resolution_x", resolutionX).With("resolution_y", resolutionY).With("percentage", percentage)
                    .With("samples", samples).With("denoise", denoise).With("device", device)
                    .With("film_transparent", filmTransparent).With("filter_size", filterSize).With("camera", camera).With("scene", scene),
                cancellationToken)
            : Task.FromResult(ToolResponse.Failure(Messages.ResolutionTooLarge, Messages.ResolutionHint));

    [McpServerTool(Name = "blender_render_image", Destructive = false, OpenWorld = false)]
    [Description(ToolDescriptions.Rendering.Image)]
    public async Task<CallToolResult> ImageAsync(
        [Description("Absolute path of the image to write; the extension follows the output format.")] string path,
        [Description("Frame to render; the current frame otherwise.")] int? frame = null,
        [Description(ToolDescriptions.Parameters.Scene)] string? scene = null,
        [Description("View layer to render alone; every enabled layer otherwise.")] string? viewLayer = null,
        [Description(ToolDescriptions.Parameters.Preview)] bool preview = true,
        [Description(ToolDescriptions.Parameters.TimeoutSeconds)] int timeoutSeconds = 300,
        IProgress<ProgressNotificationValue>? progress = null,
        CancellationToken cancellationToken = default) =>
        await WithinAsync(timeoutSeconds, async timeout => ToolResponse.Pictured(gallery.Remember(await watch.FollowAsync(
            ExchangeAsync(BridgeCommands.RenderImage, new JsonObject().With("path", path).With("frame", frame).With("scene", scene).With("view_layer", viewLayer).With("preview", preview), timeout, cancellationToken),
            progress, 1, cancellationToken).ConfigureAwait(false)))).ConfigureAwait(false);

    [McpServerTool(Name = "blender_render_animation", Destructive = false, OpenWorld = false)]
    [Description(ToolDescriptions.Rendering.Animation)]
    public async Task<CallToolResult> AnimationAsync(
        [Description("Output path: a folder with a prefix for image sequences (/renders/shot_ or /renders/shot_####), a file for FFMPEG. Blender adds the frame number and the extension; a finished file name such as shot_0001.png is refused, pass shot_####.png.")] string outputPath,
        [Description("First frame; the scene start otherwise.")] int? start = null,
        [Description("Last frame; the scene end otherwise.")] int? end = null,
        [Description("Render every nth frame.")] int? step = null,
        [Description("PNG, JPEG, OPEN_EXR or FFMPEG for a video.")] string? fileFormat = null,
        [Description(ToolDescriptions.Parameters.Scene)] string? scene = null,
        [Description(ToolDescriptions.Parameters.Preview)] bool preview = true,
        [Description("Render in a second Blender, the default: this one stays free, the reply is a job id and blender_render_job_cancel stops it. False renders here, which blocks Blender until every frame is done and cannot be stopped.")] bool background = true,
        [Description(ToolDescriptions.Parameters.TimeoutSeconds)] int timeoutSeconds = 600,
        IProgress<ProgressNotificationValue>? progress = null,
        CancellationToken cancellationToken = default) =>
        background
            ? await InTheBackgroundAsync(outputPath, Frames(start, end, step), fileFormat, scene, cancellationToken).ConfigureAwait(false)
            : await WithinAsync(timeoutSeconds, async timeout => ToolResponse.Pictured(gallery.Remember(await watch.FollowAsync(
                ExchangeAsync(BridgeCommands.RenderAnimation,
                    new JsonObject().With("output_path", outputPath).With("start", start).With("end", end).With("step", step).With("file_format", fileFormat).With("scene", scene).With("preview", preview),
                    timeout, cancellationToken),
                progress, FrameCount(start, end, step), cancellationToken).ConfigureAwait(false)))).ConfigureAwait(false);

    /// <summary>The same range as a background job: one worker over the whole range, so the frames land exactly where the blocking path would put them.</summary>
    /// <remarks>The worker renders a range with <c>render(animation=True)</c> on the same output path, which is what the blocking command does too;
    /// a still is not offered this way because a job names its files after the frame and a still is asked for by its exact path.</remarks>
    private async Task<CallToolResult> InTheBackgroundAsync(string outputPath, string? frames, string? fileFormat, string? scene, CancellationToken cancellationToken)
    {
        var reply = jobs.Observe(await ExchangeAsync(BridgeCommands.RenderJob,
            new JsonObject().With("output_path", outputPath).With("frames", frames).With("file_format", fileFormat).With("scene", scene)
                .With("overwrite", true).With("placeholder", false).With("chunks", 1).With("max_parallel", 1).With("retries", 0).With("priority", 0)
                .With("ocio_config", config.OcioConfig),
            TimeSpan.FromSeconds(120), cancellationToken).ConfigureAwait(false));

        if (!reply.IsOk || reply.Result is not JsonObject job)
        {
            return ToolResponse.From(reply);
        }

        var noted = job.DeepClone().AsObject();
        noted["note"] = Messages.BackgroundRenderNote;

        return ToolResponse.Success(noted);
    }

    /// <summary>A frame range as the job spec writes it: 1-240, or 1-240x2 with a step; nothing means the scene's own range.</summary>
    private static string? Frames(int? start, int? end, int? step) =>
        start is { } first && end is { } last ? $"{first}-{last}{(step > 1 ? $"x{step}" : string.Empty)}" : null;

    [McpServerTool(Name = "blender_set_world", Destructive = false, OpenWorld = false)]
    [Description(ToolDescriptions.Rendering.World)]
    public Task<CallToolResult> WorldAsync(
        [Description(ToolDescriptions.Parameters.Color)] double[]? color = null,
        [Description("Background light strength; 1 is the default.")] double? strength = null,
        [Description("Absolute path of an HDRI (.hdr or .exr) to light the scene with; empty string removes it.")] string? hdriPath = null,
        [Description("Rotate the HDRI around the vertical axis, in degrees.")] double? rotation = null,
        [Description("Enable or disable mist (fog by distance).")] bool? mist = null,
        [Description("Distance where mist starts, in metres.")] double? mistStart = null,
        [Description("Distance over which mist reaches full density, in metres.")] double? mistDepth = null,
        CancellationToken cancellationToken = default) =>
        SendAsync(BridgeCommands.SetWorld,
            new JsonObject().With("color", color).With("strength", strength).With("hdri_path", hdriPath).With("rotation", rotation).With("mist", mist)
                .With("mist_start", mistStart).With("mist_depth", mistDepth),
            cancellationToken);

    private static int? FrameCount(int? start, int? end, int? step) =>
        start is { } first && end is { } last && last >= first ? (last - first) / Math.Max(1, step ?? 1) + 1 : null;
}
