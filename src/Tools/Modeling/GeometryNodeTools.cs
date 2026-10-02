using System.ComponentModel;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Snail.MCP.Blender.Application.Skills;
using Snail.MCP.Blender.Tools.Prompts;

namespace Snail.MCP.Blender.Tools.Modeling;

/// <summary>Geometry Nodes: procedural geometry built as a node tree on a modifier.</summary>
[McpServerToolType]
[Skill(Skills.Modeling, SkillDescriptions.Modeling)]
public sealed class GeometryNodeTools(IBlenderBridge bridge) : BridgeToolBase(bridge)
{
    [McpServerTool(Name = "blender_build_geometry_nodes", Destructive = false, OpenWorld = false)]
    [Description(ToolDescriptions.GeometryNodes.Build)]
    public Task<CallToolResult> BuildAsync(
        [Description(ToolDescriptions.Parameters.ObjectName)] string name,
        [Description("Nodes as [{\"id\": \"sub\", \"type\": \"SubdivideMesh\", \"inputs\": {\"Level\": 2}, \"properties\": {}, \"location\": [0, 0]}, ...]. Group Input and Group Output exist already.")]
        JsonArray? nodes = null,
        [Description("Links as [[\"Group Input\", \"Geometry\", \"sub\", \"Mesh\"], [\"sub\", \"Mesh\", \"Group Output\", \"Geometry\"]]: from id, output socket, to id, input socket.")]
        JsonArray? links = null,
        [Description("Extra tree inputs exposed on the modifier: [{\"name\": \"Count\", \"socket_type\": \"NodeSocketInt\"}].")] JsonArray? interfaceInputs = null,
        [Description("Values for the tree's inputs by name, e.g. {\"Count\": 5}.")] JsonObject? inputs = null,
        [Description("Name of the Geometry Nodes modifier to build on; the first one, or a new one, otherwise.")] string? modifier = null,
        [Description("Remove every node except Group Input and Group Output first.")] bool clear = false,
        CancellationToken cancellationToken = default) =>
        SendAsync(BridgeCommands.BuildGeometryNodes,
            new JsonObject().With("name", name).With("nodes", nodes).With("links", links).With("interface_inputs", interfaceInputs).With("inputs", inputs)
                .With("modifier", modifier).With("clear", clear),
            cancellationToken);

    [McpServerTool(Name = "blender_set_geometry_inputs", Destructive = false, OpenWorld = false)]
    [Description(ToolDescriptions.GeometryNodes.SetInputs)]
    public Task<CallToolResult> SetInputsAsync(
        [Description(ToolDescriptions.Parameters.ObjectName)] string name,
        [Description("Values by input name; objects are named by their name.")] JsonObject inputs,
        [Description("Name of the Geometry Nodes modifier; the first one otherwise.")] string? modifier = null,
        CancellationToken cancellationToken = default) =>
        SendAsync(BridgeCommands.SetGeometryInputs, new JsonObject().With("name", name).With("inputs", inputs).With("modifier", modifier), cancellationToken);
}
