namespace Snail.MCP.Blender.Domain.Commands;

/// <summary>Commands of <c>addon/jobs.py</c>: background render jobs, their queue and the pre-flight check.</summary>
public static partial class BridgeCommands
{
    public static readonly BridgeCommand RenderJob = new("render_job", AddOnModules.Jobs);

    public static readonly BridgeCommand RenderJobStatus = new("render_job_status", AddOnModules.Jobs);

    public static readonly BridgeCommand RenderJobCancel = new("render_job_cancel", AddOnModules.Jobs);

    public static readonly BridgeCommand RenderCheck = new("render_check", AddOnModules.Jobs);
}
