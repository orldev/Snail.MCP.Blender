using System.Text.Json;
using Snail.MCP.Blender.Configuration;

namespace Snail.MCP.Blender.Application.Discovery;

/// <summary>What each command means, read at run time from the schema both sides share.</summary>
/// <remarks>The guard tests read <c>addon/commands.json</c> to hold the tools' annotations to it; this reads the same file for the decisions
/// that have to be made while the server runs. A tool's annotation answers for a tool, and three tools — <c>blender_run</c>,
/// <c>blender_batch</c> and <c>blender_program</c> — carry commands they are handed, so their own annotation says nothing about the work
/// they are about to do. The schema does.
/// <para>A file that cannot be read leaves every command unknown rather than stopping the server: the caller decides what an unknown command
/// costs, and for the scopes that is the widest ordinary answer, never the narrowest.</para></remarks>
public sealed class CommandSemantics
{
    private readonly IReadOnlyDictionary<string, JsonObject> _commands;

    public CommandSemantics(string? addOnDirectory = null)
    {
        _commands = Read(Path.Combine(addOnDirectory ?? ServerPaths.BundledAddOnDirectory, "commands.json"));
    }

    /// <summary>Whether the schema was found at all; without it every answer here is a guess and the caller may want to say so.</summary>
    public bool IsKnown => _commands.Count > 0;

    /// <summary>Only reads, whatever it is asked to do; a command read-only for some of its actions only is not one of these.</summary>
    public bool IsReadOnly(string command) => Flag(command, "read_only");

    /// <summary>Can lose work: deletes, clears, replaces or undoes.</summary>
    public bool IsDestructive(string command) => Flag(command, "destructive");

    /// <summary>Reaches past Blender: the file system, another process, the network.</summary>
    public bool IsOpenWorld(string command) => Flag(command, "open_world");

    private bool Flag(string command, string name) =>
        _commands.TryGetValue(command, out var meaning) && meaning[name] is JsonValue value && value.TryGetValue<bool>(out var set) && set;

    private static IReadOnlyDictionary<string, JsonObject> Read(string file)
    {
        try
        {
            return (JsonNode.Parse(File.ReadAllText(file))?["commands"] as JsonObject ?? [])
                .Where(command => command.Value is JsonObject)
                .ToDictionary(command => command.Key, command => (JsonObject)command.Value!, StringComparer.Ordinal);
        }
        catch (Exception failure) when (failure is IOException or JsonException or UnauthorizedAccessException)
        {
            return new Dictionary<string, JsonObject>(StringComparer.Ordinal);
        }
    }
}
