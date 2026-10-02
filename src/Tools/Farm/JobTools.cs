using System.ComponentModel;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Snail.MCP.Blender.Application.Rendering;
using Snail.MCP.Blender.Application.Skills;
using Snail.MCP.Blender.Configuration;
using Snail.MCP.Blender.Tools.Prompts;

namespace Snail.MCP.Blender.Tools.Farm;

/// <summary>Renders that outlive a tool call: background jobs in a second Blender, their progress, and the check before them.</summary>
[McpServerToolType]
[Skill(Skills.Farm, SkillDescriptions.Farm)]
public sealed class JobTools(IBlenderBridge bridge, ServerConfig config, RenderJobs jobs)
{
    [McpServerTool(Name = "blender_render_job", Destructive = false, OpenWorld = false)]
    [Description(ToolDescriptions.Jobs.Start)]
    public Task<CallToolResult> StartAsync(
        [Description("Output path template: folder and prefix with #### for the frame number, {scene} and {camera} for their names, e.g. /renders/{scene}/{camera}_####.")] string outputPath,
        [Description(ToolDescriptions.Parameters.Frames)] string? frames = null,
        [Description("Cameras to render one after another, each into its own files through {camera}; the scene camera otherwise.")] string[]? cameras = null,
        [Description("View layers to render; every enabled layer otherwise.")] string[]? viewLayers = null,
        [Description("EEVEE, CYCLES or WORKBENCH; the scene's engine otherwise.")] string? engine = null,
        [Description("What the job writes: format, depth, codec, size, samples, overwrite and placeholders.")] JobOutput? output = null,
        [Description("How the job runs: chunks, parallel jobs, retries, priority, threads and OCIO config.")] JobRun? queue = null,
        [Description(ToolDescriptions.Parameters.Scene)] string? scene = null,
        CancellationToken cancellationToken = default) =>
        ToolLimits.IsRenderable(output?.ResolutionX) && ToolLimits.IsRenderable(output?.ResolutionY)
            ? TrackAsync(BridgeCommands.RenderJob,
                new JsonObject().With("output_path", outputPath).With("frames", frames).With("cameras", cameras).With("view_layers", viewLayers).With("engine", engine)
                    .With("file_format", output?.FileFormat).With("color_depth", output?.ColorDepth).With("exr_codec", output?.ExrCodec).With("color_mode", output?.ColorMode)
                    .With("resolution_x", output?.ResolutionX).With("resolution_y", output?.ResolutionY).With("percentage", output?.Percentage).With("samples", output?.Samples)
                    .With("overwrite", output?.Overwrite ?? true).With("placeholder", output?.Placeholder ?? false)
                    .With("threads", queue?.Threads).With("chunks", queue?.Chunks ?? 1).With("max_parallel", queue?.MaxParallel ?? 1).With("retries", queue?.Retries ?? 0)
                    .With("priority", queue?.Priority ?? 0).With("ocio_config", queue?.OcioConfig ?? config.OcioConfig).With("scene", scene),
                TimeSpan.FromSeconds(120), cancellationToken)
            : Task.FromResult(ToolResponse.Failure(Messages.ResolutionTooLarge, Messages.ResolutionHint));

    [McpServerTool(Name = "blender_render_job_status", ReadOnly = true, OpenWorld = false)]
    [Description(ToolDescriptions.Jobs.Status)]
    public Task<CallToolResult> StatusAsync(
        [Description(ToolDescriptions.Parameters.RenderJobId)] string? id = null,
        [Description("Lines from the end of the job's log to include.")] int logLines = 20,
        CancellationToken cancellationToken = default) =>
        TrackAsync(BridgeCommands.RenderJobStatus, new JsonObject().With("id", id).With("log_lines", logLines), null, cancellationToken);

    [McpServerTool(Name = "blender_render_job_cancel", Destructive = true, OpenWorld = false)]
    [Description(ToolDescriptions.Jobs.Cancel)]
    public Task<CallToolResult> CancelAsync(
        [Description(ToolDescriptions.Parameters.RenderJobId)] string id,
        CancellationToken cancellationToken = default) =>
        TrackAsync(BridgeCommands.RenderJobCancel, new JsonObject().With("id", id), null, cancellationToken);

    [McpServerTool(Name = "blender_render_check", ReadOnly = true, OpenWorld = false)]
    [Description(ToolDescriptions.Jobs.Check)]
    public async Task<CallToolResult> CheckAsync(
        [Description("Output path to check instead of the scene's; a template with #### works.")] string? outputPath = null,
        [Description(ToolDescriptions.Parameters.Scene)] string? scene = null,
        CancellationToken cancellationToken = default) =>
        ToolResponse.From(await bridge.SendAsync(BridgeCommands.RenderCheck, new JsonObject().With("output_path", outputPath).With("scene", scene), null, cancellationToken).ConfigureAwait(false));

    private async Task<CallToolResult> TrackAsync(BridgeCommand command, JsonObject arguments, TimeSpan? timeout, CancellationToken cancellationToken) =>
        ToolResponse.From(jobs.Observe(await bridge.SendAsync(command, arguments, timeout, cancellationToken).ConfigureAwait(false)));
}
