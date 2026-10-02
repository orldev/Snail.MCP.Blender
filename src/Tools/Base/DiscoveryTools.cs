using System.ComponentModel;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Snail.MCP.Blender.Application.Discovery;
using Snail.MCP.Blender.Application.Skills;
using Snail.MCP.Blender.Tools.Prompts;

namespace Snail.MCP.Blender.Tools.Base;

/// <summary>Finding the tool for a task across the whole catalog, loaded skills or not, and loading what it takes to call it.</summary>
/// <remarks>Without this a model sees the base tools plus blender_python and concludes, correctly for what it can see, that a script
/// is the only way to bevel an edge. Search reads the same attributes the tools are registered from, so it cannot promise a tool
/// that does not exist.</remarks>
[McpServerToolType]
public sealed class DiscoveryTools(ToolIndex index, SkillCatalog skills)
{
    [McpServerTool(Name = "blender_find_tool", Destructive = false, OpenWorld = false)]
    [Description(ToolDescriptions.Discovery.FindTool)]
    public async Task<CallToolResult> FindAsync(
        [Description("What you are trying to do, in English words: \"bevel the edges of a mesh\", \"warm key light on the subject\", \"render four frames in the background\".")] string intent,
        [Description("Most tools to return, 1 to 25; 8 by default.")] int limit = 8,
        [Description("Load the skills the matches live in, so their tools join the tool list; false by default, because the answer already carries what it takes to call them through blender_program.")] bool enable = false,
        McpServer? server = null,
        CancellationToken cancellationToken = default)
    {
        var matches = index.Search(intent, Math.Clamp(limit, 1, 25));

        if (matches.Count == 0)
        {
            return ToolResponse.Failure(Messages.NoToolFor(intent), Messages.NoToolForHint);
        }

        var loaded = enable ? Load(matches) : [];

        if (loaded.Count > 0)
        {
            await ToolListNotice.SendAsync(server, cancellationToken).ConfigureAwait(false);
        }

        return ToolResponse.Success(new JsonObject
        {
            ["intent"] = intent,
            ["tools"] = new JsonArray([.. matches.Select(Describe)]),
            ["enabledSkills"] = new JsonArray([.. loaded.Select(name => (JsonNode)name)]),
        });
    }

    /// <summary>Loads every skill the matches came from that is not loaded yet, so the tools just named can be called.</summary>
    private IReadOnlyList<string> Load(IEnumerable<ToolMatch> matches) =>
        [.. matches
            .Select(match => match.Tool.Skill)
            .OfType<string>()
            .Distinct(StringComparer.Ordinal)
            .Where(skill => skills.Enable(skill) is { IsEnabled: true })];

    private JsonNode Describe(ToolMatch match) => new JsonObject
    {
        ["tool"] = match.Tool.Name,
        ["description"] = match.Tool.Description,
        ["skill"] = match.Tool.Skill,
        ["loaded"] = match.Tool.Skill is null || IsLoaded(match.Tool.Skill),
        ["command"] = match.Tool.Command,
        ["parameters"] = new JsonArray([.. match.Tool.Parameters.Select(parameter => (JsonNode)new JsonObject
        {
            ["name"] = parameter.Name,
            ["type"] = parameter.Type,
            ["required"] = parameter.IsRequired,
            ["description"] = parameter.Description,
        })]),
        ["match"] = match.Score,
    };

    private bool IsLoaded(string skill) =>
        skills.Status().Any(state => string.Equals(state.Name, skill, StringComparison.Ordinal) && state.IsEnabled);
}
