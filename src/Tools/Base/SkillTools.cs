using System.ComponentModel;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Snail.MCP.Blender.Application.Skills;
using Snail.MCP.Blender.Tools.Prompts;

namespace Snail.MCP.Blender.Tools.Base;

/// <summary>Loading and unloading skills: the on-demand tool groups.</summary>
[McpServerToolType]
public sealed class SkillTools(SkillCatalog skills)
{
    [McpServerTool(Name = "blender_skills", ReadOnly = true, OpenWorld = false)]
    [Description(ToolDescriptions.Skills.List)]
    public CallToolResult List() =>
        ToolResponse.Success(new JsonObject { ["skills"] = new JsonArray([.. skills.Status().Select(Describe)]) });

    [McpServerTool(Name = "blender_enable_skill", Destructive = false, OpenWorld = false)]
    [Description(ToolDescriptions.Skills.Enable)]
    public async Task<CallToolResult> EnableAsync(
        [Description(ToolDescriptions.Parameters.SkillName)] string name,
        McpServer? server = null,
        CancellationToken cancellationToken = default)
    {
        if (skills.Enable(name) is not { } state)
        {
            return Unknown(name);
        }

        await ToolListNotice.SendAsync(server, cancellationToken).ConfigureAwait(false);

        return ToolResponse.Success(Describe(state));
    }

    [McpServerTool(Name = "blender_disable_skill", Destructive = false, OpenWorld = false)]
    [Description(ToolDescriptions.Skills.Disable)]
    public async Task<CallToolResult> DisableAsync(
        [Description(ToolDescriptions.Parameters.SkillName)] string name,
        McpServer? server = null,
        CancellationToken cancellationToken = default)
    {
        if (skills.Disable(name) is not { } state)
        {
            return Unknown(name);
        }

        await ToolListNotice.SendAsync(server, cancellationToken).ConfigureAwait(false);

        return ToolResponse.Success(Describe(state));
    }

    private CallToolResult Unknown(string name) =>
        ToolResponse.Failure(Messages.UnknownSkill(name), Messages.KnownSkills(skills.Skills.Select(skill => skill.Name)));

    private static JsonNode Describe(SkillState state) => new JsonObject
    {
        ["name"] = state.Name,
        ["description"] = state.Description,
        ["enabled"] = state.IsEnabled,
        ["tools"] = new JsonArray([.. state.Tools.Select(tool => (JsonNode)tool)]),
    };
}
