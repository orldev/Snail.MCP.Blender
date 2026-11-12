using System.Reflection;

namespace Snail.MCP.Blender.Domain.Commands;

/// <summary>A command the add-on understands: its wire name, the add-on module that implements it and the link that carries it.</summary>
internal sealed record BridgeCommand(string Name, string Module, BridgeChannel Channel = BridgeChannel.Control);

/// <summary>Which of the three links to the add-on carries a command; the contract test keeps every channel equal to the <c>channel</c> of <c>addon/commands.json</c>.</summary>
/// <remarks>The add-on runs the control channel on Blender's main thread, one command after another, so a command on it can wait behind
/// a render that has already started. The other two are answered from the socket thread, and each has a socket of its own: that is what
/// keeps a download and a heartbeat out of that queue. A command answered off the main thread over there but sent on the control link
/// here would queue behind the render all the same, which is why the channel is written down rather than left to the call site.</remarks>
internal enum BridgeChannel
{
    /// <summary>Blender's main thread: everything that touches the scene.</summary>
    Control,

    /// <summary>Files and the data directory.</summary>
    Data,

    /// <summary>The heartbeat and the progress of the render in flight.</summary>
    Monitor,
}

/// <summary>Every command the add-on registers, one partial file per add-on module; the list is read off the fields, so a command declared once is catalogued once.</summary>
/// <remarks>The contract tests keep each partial equal to the <c>@command</c> registrations of the module it names, so a command added on
/// one side and forgotten on the other fails a test rather than a call at runtime. The list is built lazily: static initialisers of
/// partial files run in no defined order, and read eagerly the fields of a later file would still be null.</remarks>
internal partial class BridgeCommands
{
    private static readonly Lazy<IReadOnlyList<BridgeCommand>> Catalog = new(() =>
    [
        .. typeof(BridgeCommands)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.FieldType == typeof(BridgeCommand))
            .Select(field => (BridgeCommand)field.GetValue(null)!),
    ]);

    public static IReadOnlyList<BridgeCommand> All => Catalog.Value;

    /// <summary>What every tool that sends one command is called before the command's name: <c>blender_add_primitive</c> sends <c>add_primitive</c>.</summary>
    public const string ToolPrefix = "blender_";

    /// <summary>The tool a command stands for, under whose name a session's gate grants it: <c>add_primitive</c> is <c>blender_add_primitive</c>.</summary>
    /// <remarks>A command no tool sends — <c>run</c>, <c>batch_job</c> — still has a tool name to be asked about; the gate answers for it from
    /// the command's own meaning.</remarks>
    public static string ToolOf(string command) => ToolPrefix + command;

    /// <summary>The command a tool sends, when the catalog holds one of its name without the prefix; null for the server's own tools.</summary>
    /// <remarks>The tools that carry no command are the server's own — discovery, skills, transfers, the add-on package — and a program cannot
    /// send those, which is what a null says.</remarks>
    public static string? CommandOf(string tool)
    {
        var name = tool.StartsWith(ToolPrefix, StringComparison.Ordinal) ? tool[ToolPrefix.Length..] : tool;

        return All.Any(command => string.Equals(command.Name, name, StringComparison.Ordinal)) ? name : null;
    }

    /// <summary>The commands one add-on module implements.</summary>
    public static IReadOnlyList<BridgeCommand> Of(string module) =>
        [.. All.Where(command => string.Equals(command.Module, module, StringComparison.Ordinal))];
}
