using System.ComponentModel;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Snail.MCP.Blender.Tools.Prompts;

namespace Snail.MCP.Blender.Tools.Base;

/// <summary>Lights: adding and tuning point, sun, spot and area lights.</summary>
[McpServerToolType]
public sealed class LightTools(IBlenderBridge bridge) : BridgeToolBase(bridge)
{
    [McpServerTool(Name = "blender_add_light", Destructive = false, OpenWorld = false)]
    [Description(ToolDescriptions.Light.AddLight)]
    public Task<CallToolResult> AddLightAsync(
        [Description(ToolDescriptions.Parameters.LightType)] string type,
        [Description("Name for the light object.")] string? name = null,
        [Description(ToolDescriptions.Parameters.Location)] double[]? location = null,
        [Description(ToolDescriptions.Parameters.Rotation)] double[]? rotation = null,
        [Description(ToolDescriptions.Parameters.LightEnergy)] double? energy = null,
        [Description(ToolDescriptions.Parameters.LightColor)] double[]? color = null,
        [Description("Cast shadows.")] bool? shadow = null,
        [Description("SPOT: cone angle in degrees.")] double? spotSize = null,
        [Description("SPOT: softness of the cone edge, 0 to 1.")] double? spotBlend = null,
        [Description("AREA: size of the emitting square in metres.")] double? size = null,
        [Description("SUN: angular diameter in degrees; larger softens shadows.")] double? angle = null,
        [Description("POINT and SPOT: radius of the emitter in metres; larger softens shadows.")] double? radius = null,
        [Description(ToolDescriptions.Parameters.LightTemperature)] double? temperature = null,
        [Description(ToolDescriptions.Parameters.LightShape)] string? shape = null,
        [Description("AREA: second size in metres for RECTANGLE and ELLIPSE.")] double? sizeY = null,
        [Description("AREA: spread angle in degrees, 1 to 180; 180 is a soft box, 30 a narrow strip.")] double? spread = null,
        [Description("Absolute path of an IES profile for a real fixture's beam; empty string removes it.")] string? iesPath = null,
        CancellationToken cancellationToken = default) =>
        SendAsync(BridgeCommands.AddLight,
            LightArguments(new JsonObject().With("type", type).With("name", name).With("location", location).With("rotation", rotation),
                energy, color, shadow, spotSize, spotBlend, size, angle, radius, temperature, shape, sizeY, spread, iesPath),
            cancellationToken);

    [McpServerTool(Name = "blender_set_light", Destructive = false, OpenWorld = false)]
    [Description(ToolDescriptions.Light.SetLight)]
    public Task<CallToolResult> SetLightAsync(
        [Description("Name of the light object.")] string name,
        [Description(ToolDescriptions.Parameters.LightEnergy)] double? energy = null,
        [Description(ToolDescriptions.Parameters.LightColor)] double[]? color = null,
        [Description("Cast shadows.")] bool? shadow = null,
        [Description("SPOT: cone angle in degrees.")] double? spotSize = null,
        [Description("SPOT: softness of the cone edge, 0 to 1.")] double? spotBlend = null,
        [Description("AREA: size of the emitting square in metres.")] double? size = null,
        [Description("SUN: angular diameter in degrees; larger softens shadows.")] double? angle = null,
        [Description("POINT and SPOT: radius of the emitter in metres; larger softens shadows.")] double? radius = null,
        [Description(ToolDescriptions.Parameters.LightTemperature)] double? temperature = null,
        [Description(ToolDescriptions.Parameters.LightShape)] string? shape = null,
        [Description("AREA: second size in metres for RECTANGLE and ELLIPSE.")] double? sizeY = null,
        [Description("AREA: spread angle in degrees, 1 to 180; 180 is a soft box, 30 a narrow strip.")] double? spread = null,
        [Description("Absolute path of an IES profile for a real fixture's beam; empty string removes it.")] string? iesPath = null,
        CancellationToken cancellationToken = default) =>
        SendAsync(BridgeCommands.SetLight,
            LightArguments(new JsonObject().With("name", name), energy, color, shadow, spotSize, spotBlend, size, angle, radius, temperature, shape, sizeY, spread, iesPath),
            cancellationToken);

    private static JsonObject LightArguments(JsonObject arguments, double? energy, double[]? color, bool? shadow,
        double? spotSize, double? spotBlend, double? size, double? angle, double? radius, double? temperature, string? shape, double? sizeY, double? spread, string? iesPath) =>
        arguments.With("energy", energy).With("color", color).With("shadow", shadow).With("spot_size", spotSize)
            .With("spot_blend", spotBlend).With("size", size).With("angle", angle).With("radius", radius)
            .With("temperature", temperature).With("shape", shape).With("size_y", sizeY).With("spread", spread).With("ies_path", iesPath);
}
