using System.ComponentModel;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Snail.MCP.Blender.Tools.Prompts;

namespace Snail.MCP.Blender.Tools.Base;

/// <summary>Cameras: adding, aiming, lens and depth of field.</summary>
[McpServerToolType]
public sealed class CameraTools(IBlenderBridge bridge) : BridgeToolBase(bridge)
{
    [McpServerTool(Name = "blender_add_camera", Destructive = false, OpenWorld = false)]
    [Description(ToolDescriptions.Camera.AddCamera)]
    public Task<CallToolResult> AddCameraAsync(
        [Description("Name for the camera object.")] string? name = null,
        [Description(ToolDescriptions.Parameters.Location)] double[]? location = null,
        [Description(ToolDescriptions.Parameters.Rotation)] double[]? rotation = null,
        [Description("Focal length in millimetres.")] double? lens = null,
        [Description("[x, y, z] point the camera turns towards; overrides rotation.")] double[]? lookAt = null,
        [Description("Make it the scene's render camera.")] bool makeActive = true,
        CancellationToken cancellationToken = default) =>
        SendAsync(BridgeCommands.AddCamera,
            new JsonObject().With("name", name).With("location", location).With("rotation", rotation)
                .With("lens", lens).With("look_at", lookAt).With("make_active", makeActive),
            cancellationToken);

    [McpServerTool(Name = "blender_set_camera", Destructive = false, OpenWorld = false)]
    [Description(ToolDescriptions.Camera.SetCamera)]
    public Task<CallToolResult> SetCameraAsync(
        [Description("Name of the camera object.")] string name,
        [Description("Focal length in millimetres.")] double? lens = null,
        [Description("Sensor width in millimetres.")] double? sensorWidth = null,
        [Description("Near clipping distance in metres.")] double? clipStart = null,
        [Description("Far clipping distance in metres.")] double? clipEnd = null,
        [Description("[x, y, z] point the camera turns towards.")] double[]? lookAt = null,
        [Description("Make it the scene's render camera.")] bool makeActive = false,
        CancellationToken cancellationToken = default) =>
        SendAsync(BridgeCommands.SetCamera,
            new JsonObject().With("name", name).With("lens", lens).With("sensor_width", sensorWidth)
                .With("clip_start", clipStart).With("clip_end", clipEnd).With("look_at", lookAt).With("make_active", makeActive),
            cancellationToken);
}
