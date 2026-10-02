using System.Reflection;
using System.Text.Json;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Snail.MCP.Blender.Application.Skills;
using Snail.MCP.Blender.Tools.Prompts;

namespace Snail.MCP.Blender.Documentation;

/// <summary>One tool exactly as the model receives it in tools/list.</summary>
public sealed record ToolEntry(string Name, string Description, JsonElement InputSchema);

/// <summary>Tools that share a declaring class and therefore a subject; skill tools carry the skill that loads them.</summary>
public sealed record ToolFamily(string Slug, string Title, string? Skill, IReadOnlyList<ToolEntry> Tools);

/// <summary>Reads the tool catalog through the SDK, so the pages carry the same names, descriptions and schemas the server sends over the wire.</summary>
public static class ToolCatalog
{
    private static readonly (string Type, string Slug, string Title)[] Families =
    [
        ("DiagnosticsTools", "diagnostics", "Diagnostics"),
        ("SkillTools", "skills", "Skills"),
        ("DiscoveryTools", "discovery", "Finding a tool"),
        ("ProgramTools", "programs", "Programs"),
        ("SequenceTools", "sequence", "Sequences"),
        ("TransferTools", "transfer", "Files between machines"),
        ("SceneTools", "scene", "Scene"),
        ("ObjectTools", "objects", "Objects"),
        ("CameraTools", "camera", "Camera"),
        ("LightTools", "light", "Light"),
        ("CollectionTools", "collections", "Collections"),
        ("FileTools", "files", "Files, projects, snapshots and undo"),
        ("TeamTools", "team", "Journal, leases and batches"),
        ("OperatorTools", "operators", "Operators and Python"),
        ("MeshTools", "mesh", "Mesh editing"),
        ("ModifierTools", "modifiers", "Modifiers"),
        ("CurveTools", "curves", "Curves"),
        ("GeometryTools", "geometry", "Geometry"),
        ("GeometryNodeTools", "geometry-nodes", "Geometry Nodes"),
        ("MaterialTools", "materials", "Materials"),
        ("NodeTools", "nodes", "Shader nodes"),
        ("TextureTools", "textures", "Textures and UVs"),
        ("KeyframeTools", "keyframes", "Keyframes"),
        ("RigTools", "rig", "Rigging"),
        ("MotionTools", "motion", "Paths and drivers"),
        ("ConstraintTools", "constraints", "Constraints"),
        ("ShapeKeyTools", "shape-keys", "Shape keys"),
        ("PhysicsTools", "physics", "Physics"),
        ("RenderTools", "rendering", "Rendering"),
        ("OutputTools", "output", "Colour management and output"),
        ("EngineTools", "engines", "Cycles, EEVEE, motion blur and Freestyle"),
        ("LayerTools", "layers", "View layers and light linking"),
        ("OpticsTools", "optics", "Camera optics"),
        ("StagingTools", "staging", "Camera moves and light rigs"),
        ("RenderFlagTools", "flags", "Object render flags"),
        ("ShotTools", "shots", "Product shots, captures and bakes"),
        ("JobTools", "jobs", "Background render jobs and the pre-flight check"),
        ("BudgetTools", "budget", "Render budget"),
        ("PostTools", "post", "Lens effects"),
        ("CompositorTools", "compositor", "Compositor"),
        ("IoTools", "io", "Import, export and assets"),
        ("SequencerTools", "video", "Video editing"),
        ("EditTools", "cuts", "Cuts, meta strips, proxies, timing and grading"),
        ("DeliveryTools", "delivery", "Encode and mixdown"),
    ];

    public static IReadOnlyList<ToolFamily> Read()
    {
        var declared = typeof(ToolDescriptions).Assembly.GetTypes()
            .Where(type => type.GetCustomAttribute<McpServerToolTypeAttribute>() is not null)
            .ToDictionary(type => type.Name, type => type, StringComparer.Ordinal);

        var known = Families.Select(family => family.Type).ToHashSet(StringComparer.Ordinal);
        var missing = declared.Keys.Where(name => !known.Contains(name)).ToList();

        if (missing.Count > 0)
        {
            throw new InvalidOperationException(
                $"these tool classes have no family in ToolCatalog.Families: {string.Join(", ", missing)}");
        }

        return [.. Families
            .Where(family => declared.ContainsKey(family.Type))
            .Select(family => new ToolFamily(
                family.Slug,
                family.Title,
                declared[family.Type].GetCustomAttribute<SkillAttribute>()?.Name,
                Describe(declared[family.Type])))];
    }

    private static IReadOnlyList<ToolEntry> Describe(Type type) =>
        [.. type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(method => method.GetCustomAttribute<McpServerToolAttribute>() is not null)
            .OrderBy(method => method.MetadataToken)
            .Select(Protocol)
            .Select(tool => new ToolEntry(tool.Name, tool.Description ?? string.Empty, tool.InputSchema))];

    private static Tool Protocol(MethodInfo method) =>
        McpServerTool.Create(method, Unreachable, new McpServerToolCreateOptions()).ProtocolTool;

    private static object Unreachable(RequestContext<CallToolRequestParams> context) =>
        throw new NotSupportedException("the exporter reads declarations only and never invokes a tool");
}
