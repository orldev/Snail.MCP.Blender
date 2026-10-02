namespace Snail.MCP.Blender.Domain.Commands;

/// <summary>Commands of <c>addon/layers.py</c>: view layers with their passes, and light linking.</summary>
public static partial class BridgeCommands
{
    public static readonly BridgeCommand ViewLayer = new("view_layer", AddOnModules.Layers);

    public static readonly BridgeCommand LightLinking = new("light_linking", AddOnModules.Layers);
}
