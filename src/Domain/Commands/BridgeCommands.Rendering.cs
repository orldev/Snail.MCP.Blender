namespace Snail.MCP.Blender.Domain.Commands;

/// <summary>Commands of <c>addon/rendering.py</c>: renders to disk and to the model's eye, captures, bakes, render settings and passes.</summary>
public static partial class BridgeCommands
{
    public static readonly BridgeCommand RenderSettings = new("render_settings", AddOnModules.Rendering);

    public static readonly BridgeCommand RenderImage = new("render_image", AddOnModules.Rendering);

    public static readonly BridgeCommand RenderAnimation = new("render_animation", AddOnModules.Rendering);

    public static readonly BridgeCommand RenderPasses = new("render_passes", AddOnModules.Rendering);

    public static readonly BridgeCommand RenderObject = new("render_object", AddOnModules.Rendering);

    public static readonly BridgeCommand ViewportCapture = new("viewport_capture", AddOnModules.Rendering);

    public static readonly BridgeCommand BakeTexture = new("bake_texture", AddOnModules.Rendering);
}
