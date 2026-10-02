namespace Snail.MCP.Blender.Domain.Commands;

/// <summary>Commands of <c>addon/materials.py</c>: materials, shader node graphs, textures, UVs and viewport shading.</summary>
public static partial class BridgeCommands
{
    public static readonly BridgeCommand CreateMaterial = new("create_material", AddOnModules.Materials);

    public static readonly BridgeCommand SetMaterial = new("set_material", AddOnModules.Materials);

    public static readonly BridgeCommand AssignMaterial = new("assign_material", AddOnModules.Materials);

    public static readonly BridgeCommand ListMaterials = new("list_materials", AddOnModules.Materials);

    public static readonly BridgeCommand MaterialInfo = new("material_info", AddOnModules.Materials);

    public static readonly BridgeCommand AddNode = new("add_node", AddOnModules.Materials);

    public static readonly BridgeCommand SetNode = new("set_node", AddOnModules.Materials);

    public static readonly BridgeCommand RemoveNode = new("remove_node", AddOnModules.Materials);

    public static readonly BridgeCommand LinkNodes = new("link_nodes", AddOnModules.Materials);

    public static readonly BridgeCommand BuildNodeGraph = new("build_node_graph", AddOnModules.Materials);

    public static readonly BridgeCommand LoadImageTexture = new("load_image_texture", AddOnModules.Materials);

    public static readonly BridgeCommand ProceduralTexture = new("procedural_texture", AddOnModules.Materials);

    public static readonly BridgeCommand UnwrapUv = new("unwrap_uv", AddOnModules.Materials);

    public static readonly BridgeCommand SetViewportShading = new("set_viewport_shading", AddOnModules.Materials);
}
