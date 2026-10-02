using System.ComponentModel;
using System.Reflection;
using ModelContextProtocol.Server;
using Snail.MCP.Blender.Application.Skills;

namespace Snail.MCP.Blender.Application.Discovery;

/// <summary>One parameter of a tool, as the answer to a search states it.</summary>
public sealed record ToolParameter(string Name, string Type, string? Description, bool IsRequired);

/// <summary>A tool as discovery sees it: the name the model would call, what the catalog says it does, the skill it arrives with, the command a
/// program sends for it, and the parameters either takes.</summary>
/// <remarks>The parameters travel with a search so that finding a tool is enough to call it: through blender_program by its command name, without
/// loading the skill and so without changing the tool list, which a client would have to read again.</remarks>
public sealed record ToolEntry(string Name, string Description, string? Skill, string? Command = null, IReadOnlyList<ToolParameter>? Parameters = null)
{
    public IReadOnlyList<ToolParameter> Parameters { get; } = Parameters ?? [];

    /// <summary>The words of the name; a search that matches here means far more than one that matches the prose.</summary>
    public IReadOnlySet<string> Words { get; } = Vocabulary.Of(Name);
}

/// <summary>A tool a search found, with how well it answered.</summary>
public sealed record ToolMatch(ToolEntry Tool, double Score);

/// <summary>Every tool the server can offer, skills included, searchable by what the caller is trying to do.</summary>
/// <remarks>The tool list a client holds shows only the loaded skills, so a model that has not opened a skill cannot see the tool it
/// needs and reaches for blender_python instead. This index is read from the same attributes the server registers tools from, so it
/// answers for the whole catalog whatever is loaded, and cannot drift from it.</remarks>
public sealed class ToolIndex(Assembly assembly)
{
    private readonly IReadOnlyList<ToolEntry> _tools = Read(assembly);

    public IReadOnlyList<ToolEntry> Tools => _tools;

    /// <summary>The tools that best answer an intent in plain words, most relevant first; empty when nothing matches.</summary>
    public IReadOnlyList<ToolMatch> Search(string intent, int limit)
    {
        var words = Vocabulary.Of(intent);

        return words.Count == 0
            ? []
            : [.. _tools
                .Select(tool => new ToolMatch(tool, Score(tool, words)))
                .Where(match => match.Score > 0)
                .OrderByDescending(match => match.Score)
                .ThenBy(match => match.Tool.Name, StringComparer.Ordinal)
                .Take(limit)];
    }

    /// <summary>The tool that covers a bpy operation such as <c>mesh.bevel</c>: every word of the operation stands in the tool's name, or nothing does.</summary>
    /// <remarks>Deliberately strict. Advice that names the wrong tool is worse than no advice, so a near miss answers nothing.</remarks>
    public ToolEntry? Covering(string operation)
    {
        var words = Vocabulary.Of(operation);

        return words.Count == 0
            ? null
            : _tools
                .Where(tool => words.All(tool.Words.Contains))
                .OrderBy(tool => tool.Words.Count)
                .ThenBy(tool => tool.Name, StringComparer.Ordinal)
                .FirstOrDefault();
    }

    public ToolEntry? Named(string name) =>
        _tools.FirstOrDefault(tool => string.Equals(tool.Name, name, StringComparison.Ordinal));

    /// <summary>A word in the name is worth four times one in the prose: tools here are named after what they do.</summary>
    private static double Score(ToolEntry tool, IReadOnlySet<string> words)
    {
        var score = 0.0;

        foreach (var word in words)
        {
            if (tool.Words.Contains(word))
            {
                score += 4;
            }
            else if (tool.Words.Any(part => Shares(part, word)))
            {
                score += 2.5;
            }
            else if (tool.Description.Contains(word, StringComparison.OrdinalIgnoreCase))
            {
                score += 1;
            }
        }

        return Math.Round(score / words.Count, 2);
    }

    /// <summary>The command a program sends for this tool: the tool's name without the prefix, when the catalog holds one of that name.</summary>
    /// <remarks>The tools that carry no command are the server's own — discovery, skills, transfers, the add-on package — and a program cannot
    /// send those, which is what a null says.</remarks>
    private static string? CommandOf(string tool)
    {
        var name = tool.StartsWith("blender_", StringComparison.Ordinal) ? tool["blender_".Length..] : tool;

        return BridgeCommands.All.Any(command => string.Equals(command.Name, name, StringComparison.Ordinal)) ? name : null;
    }

    private static bool IsAnArgument(ParameterInfo parameter) =>
        parameter.ParameterType != typeof(CancellationToken) && parameter.ParameterType.Name != "McpServer";

    private static ToolParameter Describe(ParameterInfo parameter) => new(
        parameter.Name ?? string.Empty,
        WireType(parameter.ParameterType),
        parameter.GetCustomAttribute<DescriptionAttribute>()?.Description,
        !parameter.HasDefaultValue);

    /// <summary>The type as the wire carries it, which is what a program has to write.</summary>
    private static string WireType(Type type)
    {
        var bare = Nullable.GetUnderlyingType(type) ?? type;

        if (bare == typeof(string) || bare.IsEnum)
        {
            return "string";
        }

        if (bare == typeof(bool))
        {
            return "boolean";
        }

        if (bare == typeof(int) || bare == typeof(long) || bare == typeof(double) || bare == typeof(float))
        {
            return "number";
        }

        return bare.IsArray || bare == typeof(JsonArray) ? "array" : "object";
    }

    /// <summary>One word is a form of the other: "material" answers "materials", "render" answers "rendering".</summary>
    private static bool Shares(string part, string word) =>
        part.Length >= 4 && word.Length >= 4
        && (part.StartsWith(word, StringComparison.OrdinalIgnoreCase) || word.StartsWith(part, StringComparison.OrdinalIgnoreCase));

    private static IReadOnlyList<ToolEntry> Read(Assembly source) =>
        [.. source.GetTypes()
            .Where(type => type.GetCustomAttribute<McpServerToolTypeAttribute>() is not null)
            .SelectMany(type => type
                .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Select(method => (Method: method, Tool: method.GetCustomAttribute<McpServerToolAttribute>(), Text: method.GetCustomAttribute<DescriptionAttribute>(), Skill: type.GetCustomAttribute<SkillAttribute>()))
                .Where(found => found.Tool?.Name is not null)
                .Select(found => new ToolEntry(
                    found.Tool!.Name!,
                    found.Text?.Description ?? string.Empty,
                    found.Skill?.Name,
                    CommandOf(found.Tool!.Name!),
                    [.. found.Method.GetParameters().Where(IsAnArgument).Select(Describe)])))
            .OrderBy(tool => tool.Name, StringComparer.Ordinal)];
}
