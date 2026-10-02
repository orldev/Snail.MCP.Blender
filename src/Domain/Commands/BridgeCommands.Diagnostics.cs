namespace Snail.MCP.Blender.Domain.Commands;

/// <summary>Commands of <c>addon/diagnostics.py</c>: whether Blender is alive and what the render in flight is doing, answered from the socket thread.</summary>
public static partial class BridgeCommands
{
    public static readonly BridgeCommand Ping = new("ping", AddOnModules.Diagnostics, BridgeChannel.Monitor);

    public static readonly BridgeCommand Progress = new("progress", AddOnModules.Diagnostics, BridgeChannel.Monitor);
}
