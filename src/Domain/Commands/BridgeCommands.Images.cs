namespace Snail.MCP.Blender.Domain.Commands;

/// <summary>Commands of <c>addon/images.py</c>: pictures the model can look at, a small preview of an image file with its exposure statistics.</summary>
public static partial class BridgeCommands
{
    public static readonly BridgeCommand InspectImage = new("inspect_image", AddOnModules.Images);
}
