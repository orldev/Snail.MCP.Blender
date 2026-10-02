namespace Snail.MCP.Blender.Domain.Commands;

/// <summary>Commands of <c>addon/visibility.py</c>: per-object render flags and ray visibility.</summary>
public static partial class BridgeCommands
{
    public static readonly BridgeCommand ObjectRenderFlags = new("object_render_flags", AddOnModules.Visibility);
}
