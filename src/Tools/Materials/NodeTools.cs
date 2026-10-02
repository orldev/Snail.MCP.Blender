using System.ComponentModel;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Snail.MCP.Blender.Application.Skills;
using Snail.MCP.Blender.Tools.Prompts;

namespace Snail.MCP.Blender.Tools.Materials;

/// <summary>The shader node graph of a material, node by node or as one specification.</summary>
[McpServerToolType]
[Skill(Skills.Materials, SkillDescriptions.Materials)]
public sealed class NodeTools(IBlenderBridge bridge) : BridgeToolBase(bridge)
{
    [McpServerTool(Name = "blender_add_node", Destructive = false, OpenWorld = false)]
    [Description(ToolDescriptions.Nodes.Add)]
    public Task<CallToolResult> AddAsync(
        [Description(ToolDescriptions.Parameters.MaterialName)] string material,
        [Description(ToolDescriptions.Parameters.NodeType)] string type,
        [Description("Name for the node; Blender's default otherwise.")] string? name = null,
        [Description(ToolDescriptions.Parameters.NodeInputs)] JsonObject? inputs = null,
        [Description(ToolDescriptions.Parameters.NodeProperties)] JsonObject? properties = null,
        [Description("[x, y] position in the node editor.")] double[]? location = null,
        CancellationToken cancellationToken = default) =>
        SendAsync(BridgeCommands.AddNode,
            new JsonObject().With("material", material).With("type", type).With("name", name).With("inputs", inputs).With("properties", properties).With("location", location),
            cancellationToken);

    [McpServerTool(Name = "blender_set_node", Destructive = false, OpenWorld = false)]
    [Description(ToolDescriptions.Nodes.Set)]
    public Task<CallToolResult> SetAsync(
        [Description(ToolDescriptions.Parameters.MaterialName)] string material,
        [Description(ToolDescriptions.Parameters.NodeName)] string node,
        [Description(ToolDescriptions.Parameters.NodeInputs)] JsonObject? inputs = null,
        [Description(ToolDescriptions.Parameters.NodeProperties)] JsonObject? properties = null,
        [Description("Rename the node.")] string? newName = null,
        [Description("Label shown on the node.")] string? label = null,
        CancellationToken cancellationToken = default) =>
        SendAsync(BridgeCommands.SetNode,
            new JsonObject().With("material", material).With("node", node).With("inputs", inputs).With("properties", properties).With("new_name", newName).With("label", label),
            cancellationToken);

    [McpServerTool(Name = "blender_link_nodes", Destructive = false, OpenWorld = false)]
    [Description(ToolDescriptions.Nodes.Link)]
    public Task<CallToolResult> LinkAsync(
        [Description(ToolDescriptions.Parameters.MaterialName)] string material,
        [Description("Source node name.")] string fromNode,
        [Description("Output socket name on the source, e.g. Color, Fac, BSDF.")] string fromSocket,
        [Description("Target node name.")] string toNode,
        [Description("Input socket name on the target, e.g. Base Color, Roughness, Surface.")] string toSocket,
        CancellationToken cancellationToken = default) =>
        SendAsync(BridgeCommands.LinkNodes,
            new JsonObject().With("material", material).With("from_node", fromNode).With("from_socket", fromSocket).With("to_node", toNode).With("to_socket", toSocket),
            cancellationToken);

    [McpServerTool(Name = "blender_remove_node", Destructive = true, OpenWorld = false)]
    [Description(ToolDescriptions.Nodes.Remove)]
    public Task<CallToolResult> RemoveAsync(
        [Description(ToolDescriptions.Parameters.MaterialName)] string material,
        [Description(ToolDescriptions.Parameters.NodeName)] string node,
        CancellationToken cancellationToken = default) =>
        SendAsync(BridgeCommands.RemoveNode, new JsonObject().With("material", material).With("node", node), cancellationToken);

    [McpServerTool(Name = "blender_build_node_graph", Destructive = false, OpenWorld = false)]
    [Description(ToolDescriptions.Nodes.Build)]
    public Task<CallToolResult> BuildAsync(
        [Description(ToolDescriptions.Parameters.MaterialName)] string material,
        [Description("Nodes as [{\"id\": \"noise\", \"type\": \"TexNoise\", \"inputs\": {\"Scale\": 8}, \"properties\": {}, \"location\": [-600, 0]}, ...]; id is how links name the node.")]
        JsonArray nodes,
        [Description("Links as [[\"noise\", \"Fac\", \"ramp\", \"Fac\"], ...]: from id, output socket, to id, input socket. Existing node names work as ids too, e.g. \"Principled BSDF\".")]
        JsonArray? links = null,
        [Description("Delete the existing nodes first; the material output and Principled BSDF must then be part of the spec.")] bool clear = false,
        CancellationToken cancellationToken = default) =>
        SendAsync(BridgeCommands.BuildNodeGraph,
            new JsonObject().With("material", material).With("nodes", nodes).With("links", links).With("clear", clear),
            cancellationToken);
}
