using System.ComponentModel;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Snail.MCP.Blender.Tools.Prompts;

namespace Snail.MCP.Blender.Tools.Base;

/// <summary>Creating, moving, copying and removing objects.</summary>
[McpServerToolType]
public sealed class ObjectTools(IBlenderBridge bridge) : BridgeToolBase(bridge)
{
    [McpServerTool(Name = "blender_add_primitive", Destructive = false, OpenWorld = false)]
    [Description(ToolDescriptions.Objects.AddPrimitive)]
    public Task<CallToolResult> AddPrimitiveAsync(
        [Description(ToolDescriptions.Parameters.PrimitiveKind)] string kind,
        [Description("Name for the new object; Blender's default otherwise.")] string? name = null,
        [Description(ToolDescriptions.Parameters.Location)] double[]? location = null,
        [Description(ToolDescriptions.Parameters.Rotation)] double[]? rotation = null,
        [Description(ToolDescriptions.Parameters.Scale)] double[]? scale = null,
        [Description("Overall size in metres: edge length for cube, plane, grid and monkey; radius for the round ones; major radius for torus.")]
        double? size = null,
        [Description("Height of a cylinder or cone.")] double? depth = null,
        [Description("Minor radius of a torus.")] double? minorRadius = null,
        CancellationToken cancellationToken = default) =>
        SendAsync(BridgeCommands.AddPrimitive,
            new JsonObject().With("kind", kind).With("name", name).With("location", location).With("rotation", rotation)
                .With("scale", scale).With("size", size).With("depth", depth).With("minor_radius", minorRadius),
            cancellationToken);

    [McpServerTool(Name = "blender_delete_objects", Destructive = true, OpenWorld = false)]
    [Description(ToolDescriptions.Objects.DeleteObjects)]
    public Task<CallToolResult> DeleteObjectsAsync(
        [Description("Names of the objects to delete.")] string[]? names = null,
        [Description("Also delete everything currently selected.")] bool selected = false,
        CancellationToken cancellationToken = default) =>
        SendAsync(BridgeCommands.DeleteObjects, new JsonObject().With("names", names).With("selected", selected), cancellationToken);

    [McpServerTool(Name = "blender_duplicate_object", Destructive = false, OpenWorld = false)]
    [Description(ToolDescriptions.Objects.DuplicateObject)]
    public Task<CallToolResult> DuplicateObjectAsync(
        [Description(ToolDescriptions.Parameters.ObjectName)] string name,
        [Description("Name for the copy; Blender appends .001 otherwise.")] string? newName = null,
        [Description("Share the mesh data with the original instead of copying it.")] bool linked = false,
        [Description("[x, y, z] offset of the copy from the original, in metres.")] double[]? offset = null,
        CancellationToken cancellationToken = default) =>
        SendAsync(BridgeCommands.DuplicateObject,
            new JsonObject().With("name", name).With("new_name", newName).With("linked", linked).With("offset", offset),
            cancellationToken);

    [McpServerTool(Name = "blender_transform_object", Destructive = false, OpenWorld = false)]
    [Description(ToolDescriptions.Objects.TransformObject)]
    public Task<CallToolResult> TransformObjectAsync(
        [Description(ToolDescriptions.Parameters.ObjectName)] string name,
        [Description(ToolDescriptions.Parameters.Location)] double[]? location = null,
        [Description(ToolDescriptions.Parameters.Rotation)] double[]? rotation = null,
        [Description(ToolDescriptions.Parameters.Scale)] double[]? scale = null,
        [Description("Apply the values on top of the current ones (location and rotation add, scale multiplies) instead of replacing them.")]
        bool relative = false,
        CancellationToken cancellationToken = default) =>
        SendAsync(BridgeCommands.TransformObject,
            new JsonObject().With("name", name).With("location", location).With("rotation", rotation).With("scale", scale).With("relative", relative),
            cancellationToken);

    [McpServerTool(Name = "blender_update_object", Destructive = false, OpenWorld = false)]
    [Description(ToolDescriptions.Objects.UpdateObject)]
    public Task<CallToolResult> UpdateObjectAsync(
        [Description(ToolDescriptions.Parameters.ObjectName)] string name,
        [Description("New name.")] string? newName = null,
        [Description("Hide or show in the viewport.")] bool? hideViewport = null,
        [Description("Exclude from or include in renders.")] bool? hideRender = null,
        [Description("Name of the new parent object.")] string? parent = null,
        [Description("Detach from the current parent.")] bool clearParent = false,
        [Description("Keep the object where it is in the world when the parent changes.")] bool keepTransform = true,
        CancellationToken cancellationToken = default) =>
        SendAsync(BridgeCommands.UpdateObject, ParentingArguments(name, newName, hideViewport, hideRender, parent, clearParent, keepTransform), cancellationToken);

    [McpServerTool(Name = "blender_add_text", Destructive = false, OpenWorld = false)]
    [Description(ToolDescriptions.Objects.AddText)]
    public Task<CallToolResult> AddTextAsync(
        [Description("The text to show.")] string body,
        [Description("Name for the text object.")] string? name = null,
        [Description("Letter height in metres.")] double? size = null,
        [Description("Depth of the extrusion in metres; 0 keeps it flat.")] double? extrude = null,
        [Description("Rounded edge depth in metres.")] double? bevelDepth = null,
        [Description("LEFT, CENTER, RIGHT, JUSTIFY or FLUSH.")] string? align = null,
        [Description("Absolute path of a .ttf or .otf font file.")] string? fontPath = null,
        [Description(ToolDescriptions.Parameters.Location)] double[]? location = null,
        [Description(ToolDescriptions.Parameters.Rotation)] double[]? rotation = null,
        [Description("Stand the text up facing -Y (rotated 90° about X) instead of lying flat on the ground.")] bool upright = true,
        CancellationToken cancellationToken = default) =>
        SendAsync(BridgeCommands.AddText,
            new JsonObject().With("body", body).With("name", name).With("size", size).With("extrude", extrude).With("bevel_depth", bevelDepth)
                .With("align", align).With("font_path", fontPath).With("location", location).With("rotation", rotation).With("upright", upright),
            cancellationToken);

    [McpServerTool(Name = "blender_add_empty", Destructive = false, OpenWorld = false)]
    [Description(ToolDescriptions.Objects.AddEmpty)]
    public Task<CallToolResult> AddEmptyAsync(
        [Description(ToolDescriptions.Parameters.EmptyType)] string type = "PLAIN_AXES",
        [Description("Name for the empty.")] string? name = null,
        [Description("Display size in metres.")] double? size = null,
        [Description(ToolDescriptions.Parameters.Location)] double[]? location = null,
        [Description(ToolDescriptions.Parameters.Rotation)] double[]? rotation = null,
        [Description("IMAGE: absolute path of the picture to show.")] string? imagePath = null,
        [Description("Parent object.")] string? parent = null,
        CancellationToken cancellationToken = default) =>
        SendAsync(BridgeCommands.AddEmpty,
            new JsonObject().With("type", type).With("name", name).With("size", size).With("location", location).With("rotation", rotation)
                .With("image_path", imagePath).With("parent", parent),
            cancellationToken);

    private static JsonObject ParentingArguments(string name, string? newName, bool? hideViewport, bool? hideRender, string? parent, bool clearParent, bool keepTransform)
    {
        var arguments = new JsonObject().With("name", name).With("new_name", newName)
            .With("hide_viewport", hideViewport).With("hide_render", hideRender).With("keep_transform", keepTransform);

        return clearParent ? arguments.WithNull("parent") : arguments.With("parent", parent);
    }
}
