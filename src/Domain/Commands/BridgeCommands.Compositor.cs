namespace Snail.MCP.Blender.Domain.Commands;

/// <summary>Commands of <c>addon/compositor.py</c>: the compositor node tree by name or spec.</summary>
public static partial class BridgeCommands
{
    public static readonly BridgeCommand Compositor = new("compositor", AddOnModules.Compositor);
}
