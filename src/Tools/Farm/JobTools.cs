using System.ComponentModel;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Snail.MCP.Blender.Application.Rendering;
using Snail.Sdk.Mcp.Hosting.Discovery;
using Snail.MCP.Blender.Configuration;
using Snail.MCP.Blender.Tools.Prompts;

namespace Snail.MCP.Blender.Tools.Farm;

/// <summary>Renders that outlive a tool call: background jobs in a second Blender, followed as jobs of the server, and the check before them.</summary>
/// <remarks>A render is a job of the server (<see cref="AddOnJobs"/>): the tool answers within the wait the client allows, with the result or
/// with the job's resource <c>job://blender/render/&lt;id&gt;</c>, whose text says where it stands; a client that can wait is told when it
/// ends. The add-on keeps the job and its files, so <c>blender_render_jobs</c> still lists what it holds across a restart of this server.</remarks>
[McpServerToolType]
[Skill(Skills.Farm, SkillDescriptions.Farm)]
public sealed class JobTools(IBlenderBridge bridge, ServerConfig config, AddOnJobs background)
{
    [McpServerTool(Name = "blender_render_job", Destructive = false, OpenWorld = false)]
    [Description(ToolDescriptions.Jobs.Start)]
    public async Task<CallToolResult> StartAsync(
        [Description("Output path template: folder and prefix with #### for the frame number, {scene} and {camera} for their names, e.g. /renders/{scene}/{camera}_####. Blender adds the extension; a finished file name such as shot_0001.png is refused, pass shot_####.png.")] string outputPath,
        [Description(ToolDescriptions.Parameters.Frames)] string? frames = null,
        [Description("Cameras to render one after another, each into its own files through {camera}; the scene camera otherwise.")] string[]? cameras = null,
        [Description("View layers to render; every enabled layer otherwise.")] string[]? viewLayers = null,
        [Description("EEVEE, CYCLES or WORKBENCH; the scene's engine otherwise.")] string? engine = null,
        [Description("What the job writes: format, depth, codec, size, samples, overwrite and placeholders.")] JobOutput? output = null,
        [Description("How the job runs: chunks, parallel jobs, retries, priority, threads and OCIO config.")] JobRun? queue = null,
        [Description(ToolDescriptions.Parameters.Scene)] string? scene = null,
        [Description(ToolDescriptions.Parameters.Wait)] int wait = 60,
        CancellationToken cancellationToken = default)
    {
        if (!ToolLimits.IsRenderable(output?.ResolutionX) || !ToolLimits.IsRenderable(output?.ResolutionY))
        {
            return ToolAnswer.Failed(Messages.ResolutionTooLarge, Messages.ResolutionHint);
        }

        var arguments = new JsonObject().With("output_path", outputPath).With("frames", frames).With("cameras", cameras).With("view_layers", viewLayers).With("engine", engine)
            .With("file_format", output?.FileFormat).With("color_depth", output?.ColorDepth).With("exr_codec", output?.ExrCodec).With("color_mode", output?.ColorMode)
            .With("resolution_x", output?.ResolutionX).With("resolution_y", output?.ResolutionY).With("percentage", output?.Percentage).With("samples", output?.Samples)
            .With("overwrite", output?.Overwrite ?? true).With("placeholder", output?.Placeholder ?? false)
            .With("threads", queue?.Threads).With("chunks", queue?.Chunks ?? 1).With("max_parallel", queue?.MaxParallel ?? 1).With("retries", queue?.Retries ?? 0)
            .With("priority", queue?.Priority ?? 0).With("ocio_config", queue?.OcioConfig ?? config.OcioConfig).With("scene", scene);
        var started = await background.StartRenderAsync(BridgeCommands.RenderJob, BridgeCommands.RenderJobStatus, arguments, cancellationToken).ConfigureAwait(false);

        return started.Job is { } job
            ? BridgeAnswers.Job(await job.ForAsync(TimeSpan.FromSeconds(wait), cancellationToken).ConfigureAwait(false))
            : BridgeAnswers.From(started.First);
    }

    [McpServerTool(Name = "blender_render_jobs", ReadOnly = true, OpenWorld = false)]
    [Description(ToolDescriptions.Jobs.List)]
    public Task<CallToolResult> ListAsync(
        [Description("Lines from the end of each job's log to include.")] int logLines = 0,
        CancellationToken cancellationToken = default) =>
        TrackAsync(BridgeCommands.RenderJobStatus, new JsonObject().With("log_lines", logLines), null, cancellationToken);

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
        BridgeAnswers.From(await bridge.SendAsync(BridgeCommands.RenderCheck, new JsonObject().With("output_path", outputPath).With("scene", scene), null, cancellationToken).ConfigureAwait(false));

    private async Task<CallToolResult> TrackAsync(BridgeCommand command, JsonObject arguments, TimeSpan? timeout, CancellationToken cancellationToken) =>
        BridgeAnswers.From(await bridge.SendAsync(command, arguments, timeout, cancellationToken).ConfigureAwait(false));
}
