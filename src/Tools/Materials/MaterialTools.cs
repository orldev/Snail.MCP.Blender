using System.ComponentModel;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Snail.MCP.Blender.Application.Skills;
using Snail.MCP.Blender.Tools.Prompts;

namespace Snail.MCP.Blender.Tools.Materials;

/// <summary>Materials as Principled BSDF settings, their assignment, and how the viewport shows them.</summary>
[McpServerToolType]
[Skill(Skills.Materials, SkillDescriptions.Materials)]
public sealed class MaterialTools(IBlenderBridge bridge) : BridgeToolBase(bridge)
{
    [McpServerTool(Name = "blender_create_material", Destructive = false, OpenWorld = false)]
    [Description(ToolDescriptions.Materials.Create)]
    public Task<CallToolResult> CreateAsync(
        [Description("Name of the new material.")] string? name = null,
        [Description("Object to assign it to right away.")] string? assignTo = null,
        [Description(ToolDescriptions.Parameters.Color)] double[]? baseColor = null,
        [Description("0 dielectric to 1 metal.")] double? metallic = null,
        [Description("0 mirror to 1 matte.")] double? roughness = null,
        [Description("Index of refraction; 1.45 glass, 1.33 water.")] double? ior = null,
        [Description("Opacity 0 to 1; below 1 needs a blend method.")] double? alpha = null,
        [Description(ToolDescriptions.Parameters.EmissionColor)] double[]? emissionColor = null,
        [Description("Emission strength; 0 is off.")] double? emissionStrength = null,
        [Description("Transmission 0 to 1; 1 for glass.")] double? transmission = null,
        [Description("Subsurface scattering weight 0 to 1.")] double? subsurface = null,
        [Description("Coat (clearcoat) weight 0 to 1.")] double? coat = null,
        [Description("Sheen weight 0 to 1.")] double? sheen = null,
        [Description("Specular level 0 to 1; 0.5 is the default.")] double? specular = null,
        [Description(ToolDescriptions.Parameters.BlendMethod)] string? blendMethod = null,
        CancellationToken cancellationToken = default) =>
        SendAsync(BridgeCommands.CreateMaterial,
            Principled(new JsonObject().With("name", name).With("assign_to", assignTo), baseColor, metallic, roughness, ior, alpha,
                emissionColor, emissionStrength, transmission, subsurface, coat, sheen, specular, blendMethod),
            cancellationToken);

    [McpServerTool(Name = "blender_set_material", Destructive = false, OpenWorld = false)]
    [Description(ToolDescriptions.Materials.Set)]
    public Task<CallToolResult> SetAsync(
        [Description(ToolDescriptions.Parameters.MaterialName)] string name,
        [Description("Rename the material.")] string? newName = null,
        [Description(ToolDescriptions.Parameters.Color)] double[]? baseColor = null,
        [Description("0 dielectric to 1 metal.")] double? metallic = null,
        [Description("0 mirror to 1 matte.")] double? roughness = null,
        [Description("Index of refraction; 1.45 glass, 1.33 water.")] double? ior = null,
        [Description("Opacity 0 to 1; below 1 needs a blend method.")] double? alpha = null,
        [Description(ToolDescriptions.Parameters.EmissionColor)] double[]? emissionColor = null,
        [Description("Emission strength; 0 is off.")] double? emissionStrength = null,
        [Description("Transmission 0 to 1; 1 for glass.")] double? transmission = null,
        [Description("Subsurface scattering weight 0 to 1.")] double? subsurface = null,
        [Description("Coat (clearcoat) weight 0 to 1.")] double? coat = null,
        [Description("Sheen weight 0 to 1.")] double? sheen = null,
        [Description("Specular level 0 to 1; 0.5 is the default.")] double? specular = null,
        [Description(ToolDescriptions.Parameters.BlendMethod)] string? blendMethod = null,
        CancellationToken cancellationToken = default) =>
        SendAsync(BridgeCommands.SetMaterial,
            Principled(new JsonObject().With("name", name).With("new_name", newName), baseColor, metallic, roughness, ior, alpha,
                emissionColor, emissionStrength, transmission, subsurface, coat, sheen, specular, blendMethod),
            cancellationToken);

    [McpServerTool(Name = "blender_assign_material", Destructive = false, OpenWorld = false)]
    [Description(ToolDescriptions.Materials.Assign)]
    public Task<CallToolResult> AssignAsync(
        [Description(ToolDescriptions.Parameters.ObjectName)] string @object,
        [Description(ToolDescriptions.Parameters.MaterialName)] string material,
        [Description("Material slot index to fill; slot 0 otherwise, and the faces follow it there.")] int? slot = null,
        [Description("Give the slot only to the mesh's selected faces (blender_mesh_select) instead of the whole object; this adds a slot rather than replacing one.")] bool selectedFaces = false,
        [Description("Add a slot at the end and leave the faces where they are, for building a mesh that wears several materials.")] bool append = false,
        CancellationToken cancellationToken = default) =>
        SendAsync(BridgeCommands.AssignMaterial,
            new JsonObject().With("object", @object).With("material", material).With("slot", slot).With("selected_faces", selectedFaces).With("append", append),
            cancellationToken);

    [McpServerTool(Name = "blender_list_materials", ReadOnly = true, OpenWorld = false)]
    [Description(ToolDescriptions.Materials.List)]
    public Task<CallToolResult> ListAsync(CancellationToken cancellationToken = default) =>
        SendAsync(BridgeCommands.ListMaterials, [], cancellationToken);

    [McpServerTool(Name = "blender_material_info", ReadOnly = true, OpenWorld = false)]
    [Description(ToolDescriptions.Materials.Info)]
    public Task<CallToolResult> InfoAsync(
        [Description(ToolDescriptions.Parameters.MaterialName)] string name,
        CancellationToken cancellationToken = default) =>
        SendAsync(BridgeCommands.MaterialInfo, new JsonObject().With("name", name), cancellationToken);

    [McpServerTool(Name = "blender_set_viewport_shading", Destructive = false, OpenWorld = false)]
    [Description(ToolDescriptions.Materials.ViewportShading)]
    public Task<CallToolResult> ViewportShadingAsync(
        [Description("WIREFRAME, SOLID, MATERIAL or RENDERED.")] string mode = "MATERIAL",
        CancellationToken cancellationToken = default) =>
        SendAsync(BridgeCommands.SetViewportShading, new JsonObject().With("mode", mode), cancellationToken);

    private static JsonObject Principled(JsonObject arguments, double[]? baseColor, double? metallic, double? roughness, double? ior, double? alpha,
        double[]? emissionColor, double? emissionStrength, double? transmission, double? subsurface, double? coat, double? sheen, double? specular,
        string? blendMethod) =>
        arguments.With("base_color", baseColor).With("metallic", metallic).With("roughness", roughness).With("ior", ior).With("alpha", alpha)
            .With("emission_color", emissionColor).With("emission_strength", emissionStrength).With("transmission", transmission)
            .With("subsurface", subsurface).With("coat", coat).With("sheen", sheen).With("specular", specular).With("blend_method", blendMethod);
}
