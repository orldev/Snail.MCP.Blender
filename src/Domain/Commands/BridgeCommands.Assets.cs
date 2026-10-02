namespace Snail.MCP.Blender.Domain.Commands;

/// <summary>Commands of <c>addon/assets.py</c>: datablocks listed in and appended from other .blend files.</summary>
public static partial class BridgeCommands
{
    public static readonly BridgeCommand ListAssets = new("list_assets", AddOnModules.Assets);

    public static readonly BridgeCommand AppendAssets = new("append_assets", AddOnModules.Assets);
}
