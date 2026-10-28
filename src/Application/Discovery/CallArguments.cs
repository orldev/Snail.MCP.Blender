using System.Text.Json;

namespace Snail.MCP.Blender.Application.Discovery;

/// <summary>What is wrong with the arguments of a tool call, read from the tool's own schema before the tool is invoked.</summary>
/// <remarks>A call that leaves out a required parameter never reaches the tool at all: binding it throws inside the SDK, and what the caller
/// reads is "An error occurred invoking 'find_tool'", which names neither the parameter nor the mistake. A month of logs holds seven such
/// calls — <c>query</c> and <c>task</c> sent for <c>intent</c>, <c>program</c> for <c>code</c>, <c>path</c> for <c>remotePath</c> — and each
/// cost a round trip to a model that could only guess again. The add-on refuses a parameter it does not read in these same words, so a caller
/// meets one sentence whichever gate it reaches first.</remarks>
public static class CallArguments
{
    /// <summary>Names offered for one unknown parameter; past two, a list reads as noise rather than as help.</summary>
    private const int Suggestions = 2;

    /// <summary>Why this call cannot be made, or null when its arguments fit the schema.</summary>
    public static string? Refusal(string tool, JsonElement schema, IEnumerable<string>? sent)
    {
        if (schema.ValueKind != JsonValueKind.Object || !schema.TryGetProperty("properties", out var properties) || properties.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var declared = properties.EnumerateObject().Select(property => property.Name).ToList();
        var given = (sent ?? []).ToList();
        var missing = Required(schema).Where(name => !given.Contains(name, StringComparer.Ordinal)).ToList();
        var unknown = given.Where(name => !declared.Contains(name, StringComparer.Ordinal)).ToList();

        if (missing.Count == 0 && unknown.Count == 0)
        {
            return null;
        }

        return Sentence(tool, missing, unknown, declared);
    }

    private static string Sentence(string tool, IReadOnlyList<string> missing, IReadOnlyList<string> unknown, IReadOnlyList<string> declared)
    {
        var needs = missing.Count == 0 ? null : $"needs {Quoted(missing)}, which this call did not send";
        var takes = unknown.Count == 0 ? null : $"takes no {string.Join(", ", unknown.Select(name => Described(name, declared)))}";
        var both = needs is null ? takes : takes is null ? needs : $"{needs}, and {takes}";

        return $"'{tool}' {both}.";
    }

    private static string Described(string name, IReadOnlyList<string> declared) =>
        Nearest(name, declared) is { Count: > 0 } nearest
            ? $"'{name}' (did you mean {string.Join(" or ", nearest.Select(hint => $"'{hint}'"))}?)"
            : $"'{name}'";

    private static string Quoted(IEnumerable<string> names) => string.Join(", ", names.Select(name => $"'{name}'"));

    private static IReadOnlyList<string> Required(JsonElement schema) =>
        schema.TryGetProperty("required", out var required) && required.ValueKind == JsonValueKind.Array
            ? [.. required.EnumerateArray().Where(name => name.ValueKind == JsonValueKind.String).Select(name => name.GetString()!)]
            : [];

    /// <summary>The declared names closest to one the tool does not take, nearest first; empty when none is close enough to be a guess.</summary>
    /// <remarks>A name that spells another badly is close by edits — <c>colour</c> for <c>color</c> — and a name that is the short form of
    /// another is close by containing it, which is how <c>timeout</c> finds <c>timeoutSeconds</c> and <c>path</c> finds <c>remotePath</c>
    /// although half a dozen edits separate them. A name that is neither offers nothing rather than the alphabetically first parameter: a
    /// wrong suggestion is worse than none.</remarks>
    private static IReadOnlyList<string> Nearest(string name, IReadOnlyList<string> declared)
    {
        var allowed = Math.Max(2, name.Length / 3);
        var scored = declared.Select(candidate => (Name: candidate, Distance: IsShortFormOf(name, candidate) ? 1 : Distance(name, candidate)));
        var close = scored.Where(candidate => candidate.Distance <= allowed).OrderBy(candidate => candidate.Distance);

        return [.. close.ThenBy(candidate => candidate.Name, StringComparer.Ordinal).Take(Suggestions).Select(candidate => candidate.Name)];
    }

    private static bool IsShortFormOf(string name, string candidate) =>
        name.Length >= 3 && candidate.Contains(name, StringComparison.OrdinalIgnoreCase);

    /// <summary>How many single-character edits turn one name into the other, ignoring case.</summary>
    private static int Distance(string from, string to)
    {
        var previous = new int[to.Length + 1];
        var current = new int[to.Length + 1];

        for (var index = 0; index <= to.Length; index++)
        {
            previous[index] = index;
        }

        for (var row = 1; row <= from.Length; row++)
        {
            current[0] = row;

            for (var column = 1; column <= to.Length; column++)
            {
                var substitution = previous[column - 1] + (char.ToLowerInvariant(from[row - 1]) == char.ToLowerInvariant(to[column - 1]) ? 0 : 1);

                current[column] = Math.Min(Math.Min(current[column - 1] + 1, previous[column] + 1), substitution);
            }

            (previous, current) = (current, previous);
        }

        return previous[to.Length];
    }
}
