namespace Snail.MCP.Blender.Domain.Commands;

/// <summary>Commands of <c>addon/modeling.py</c>: mesh edits, modifiers, curves, joins, origins and geometry nodes.</summary>
public static partial class BridgeCommands
{
    public static readonly BridgeCommand MeshSelect = new("mesh_select", AddOnModules.Modeling);

    public static readonly BridgeCommand MeshExtrude = new("mesh_extrude", AddOnModules.Modeling);

    public static readonly BridgeCommand MeshInset = new("mesh_inset", AddOnModules.Modeling);

    public static readonly BridgeCommand MeshBevel = new("mesh_bevel", AddOnModules.Modeling);

    public static readonly BridgeCommand MeshSubdivide = new("mesh_subdivide", AddOnModules.Modeling);

    public static readonly BridgeCommand MeshMerge = new("mesh_merge", AddOnModules.Modeling);

    public static readonly BridgeCommand MeshNormals = new("mesh_normals", AddOnModules.Modeling);

    public static readonly BridgeCommand MeshShade = new("mesh_shade", AddOnModules.Modeling);

    public static readonly BridgeCommand AddModifier = new("add_modifier", AddOnModules.Modeling);

    public static readonly BridgeCommand UpdateModifier = new("update_modifier", AddOnModules.Modeling);

    public static readonly BridgeCommand RemoveModifier = new("remove_modifier", AddOnModules.Modeling);

    public static readonly BridgeCommand ApplyModifier = new("apply_modifier", AddOnModules.Modeling);

    public static readonly BridgeCommand DescribeModifier = new("describe_modifier", AddOnModules.Modeling);

    public static readonly BridgeCommand AddCurve = new("add_curve", AddOnModules.Modeling);

    public static readonly BridgeCommand SetCurve = new("set_curve", AddOnModules.Modeling);

    public static readonly BridgeCommand ConvertToMesh = new("convert_to_mesh", AddOnModules.Modeling);

    public static readonly BridgeCommand JoinObjects = new("join_objects", AddOnModules.Modeling);

    public static readonly BridgeCommand SetOrigin = new("set_origin", AddOnModules.Modeling);

    public static readonly BridgeCommand ApplyTransforms = new("apply_transforms", AddOnModules.Modeling);

    public static readonly BridgeCommand BuildGeometryNodes = new("build_geometry_nodes", AddOnModules.Modeling);

    public static readonly BridgeCommand SetGeometryInputs = new("set_geometry_inputs", AddOnModules.Modeling);

    public static readonly BridgeCommand MeshGeometry = new("mesh_geometry", AddOnModules.Modeling);

    public static readonly BridgeCommand MeshSetVertices = new("mesh_set_vertices", AddOnModules.Modeling);
}
