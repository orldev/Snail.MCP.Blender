namespace Snail.MCP.Blender.Domain.Commands;

/// <summary>Commands of <c>addon/batch.py</c>: command lists run by a separate headless Blender.</summary>
public static partial class BridgeCommands
{
    public static readonly BridgeCommand BatchJob = new("batch_job", AddOnModules.Batch);

    public static readonly BridgeCommand BatchStatus = new("batch_status", AddOnModules.Batch);
}
