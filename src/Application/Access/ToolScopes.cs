using ModelContextProtocol.Server;
using Snail.MCP.Blender.Application.Discovery;
using Snail.MCP.Blender.Application.Sessions;

namespace Snail.MCP.Blender.Application.Access;

/// <summary>Which kind of work a call is, so a client's key can be asked about it before Blender is.</summary>
/// <remarks>Read from what the work already declares — the schema for a command, the annotations for a tool that sends no command of its own —
/// rather than from a list kept beside them: a new tool would be missing from a list, and a tool missing from a list is a tool nothing guards.
/// Python and the operator by name come first because both reach past every schema; the farm next, because a queued render spends the machine
/// long after the call returned.
/// <para>A tool's own name is not enough for the three tools that carry commands they are handed. <c>blender_run</c>, <c>blender_batch</c> and
/// <c>blender_program</c> are each one tool with one annotation, and what they do is whatever is inside them: a key without the Python scope
/// was running Python by sending it as a step. Those three ask about every command they carry, which is what <see cref="Refused"/> is for.</para></remarks>
public sealed class ToolScopes(CommandSemantics semantics)
{
    private static readonly string[] Farmed = ["render_job", "batch", "render_animation"];

    /// <summary>Commands that take work away from another agent although they destroy nothing of the scene.</summary>
    /// <remarks>The lease guard exists for several clients in one Blender; taking a lease another client holds is the one thing it protects
    /// against, so it belongs to the scope named for losing work rather than to the one for making it.</remarks>
    private static readonly string[] TakingFromOthers = ["lease"];

    /// <summary>The scope a call of this tool needs, when the tool is the whole of the work.</summary>
    public string For(McpServerTool tool)
    {
        var name = tool.ProtocolTool.Name;
        var command = name.StartsWith("blender_", StringComparison.Ordinal) ? name["blender_".Length..] : name;

        if (semantics.IsKnown && Known(command) is { } known)
        {
            return known;
        }

        if (Reaching(command) is { } reaching)
        {
            return reaching;
        }

        if (tool.ProtocolTool.Annotations?.DestructiveHint is true)
        {
            return Scopes.Delete;
        }

        return tool.ProtocolTool.Annotations?.ReadOnlyHint is true ? Scopes.Read : Scopes.Write;
    }

    /// <summary>The scope one command needs, for a tool that carries commands rather than being one.</summary>
    public string For(string command) => Known(command) ?? Reaching(command) ?? Scopes.Write;

    /// <summary>The first command in this list the client's key does not open, with what it would have needed; null when it opens them all.</summary>
    /// <remarks>Every step is asked about before any of them is sent: a sequence half refused is a scene half built.</remarks>
    public (string Command, string Scope)? Refused(JsonArray? commands, ClientSession client)
    {
        foreach (var step in commands?.OfType<JsonObject>() ?? [])
        {
            if (step["command"]?.GetValue<string>() is not { } command)
            {
                continue;
            }

            if (For(command) is { } scope && !client.May(scope))
            {
                return (command, scope);
            }
        }

        return null;
    }

    /// <summary>What to tell a client whose key does not open this kind of work.</summary>
    public static string Refusal(string what, string scope, string client) =>
        $"'{what}' is {scope} work, and the key '{client}' presented does not open it; ask the operator for a key with the {scope} scope.";

    /// <summary>The scopes that follow from reaching past every schema, whatever the schema says about the command itself.</summary>
    private static string? Reaching(string command) =>
        command is "python" or "run_operator" ? Scopes.Python
            : Farmed.Any(farmed => command.StartsWith(farmed, StringComparison.Ordinal)) ? Scopes.Farm
            : TakingFromOthers.Contains(command, StringComparer.Ordinal) ? Scopes.Delete
            : null;

    private string? Known(string command) =>
        Reaching(command)
        ?? (semantics.IsDestructive(command) ? Scopes.Delete
            : semantics.IsReadOnly(command) ? Scopes.Read
            : null);
}
