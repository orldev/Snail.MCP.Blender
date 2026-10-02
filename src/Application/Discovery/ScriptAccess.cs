using Snail.MCP.Blender.Configuration;

namespace Snail.MCP.Blender.Application.Discovery;

/// <summary>What the Python setting lets through: which operators run code, and whether a script is one the catalog already covers.</summary>
/// <remarks>One place for the rule, because three doors lead to the same room: blender_python, a step of blender_run or blender_batch, and a
/// command a program sends. A rule that lived at one door only was a way around the setting at the other two.</remarks>
public static class ScriptAccess
{
    /// <summary>Covered operations a script may carry before <see cref="PythonAccess.Fallback"/> refuses it: one is a fair mix, several is a sequence written as a script.</summary>
    public const int CoveredLimit = 2;

    private static readonly string[] ScriptModules = ["script.", "console.", "preferences.", "extensions."];

    /// <summary>Operators that execute code: the script module, text editor runs, the Python console, add-ons and extensions installed or enabled,
    /// and any operator asked to trust the scripts of the file it opens.</summary>
    /// <remarks>An add-on is Python: preferences.addon_install followed by preferences.addon_enable ran whatever the zip held with the setting
    /// off, and so does an extension package or a .blend opened with use_scripts.</remarks>
    public static bool RunsScripts(string operatorName, JsonObject? properties = null) =>
        ScriptModules.Any(module => operatorName.Trim().StartsWith(module, StringComparison.OrdinalIgnoreCase))
        || operatorName.Trim().Equals("text.run_script", StringComparison.OrdinalIgnoreCase)
        || properties?["use_scripts"] is JsonValue trust && trust.TryGetValue<bool>(out var isTrusted) && isTrusted;

    /// <summary>Whether a list of steps runs Python: the command itself, or an operator that runs a script.</summary>
    public static bool ContainsPython(JsonArray commands) =>
        commands.OfType<JsonObject>().Any(step =>
            step["command"]?.GetValue<string>() is { } name
            && (name == BridgeCommands.Python.Name
                || (name == BridgeCommands.RunOperator.Name && RunsScripts(step["params"]?["name"]?.GetValue<string>() ?? string.Empty, step["params"]?["params"] as JsonObject))));

    /// <summary>Whether one command may be sent under the setting, and why not when it may not.</summary>
    public static BridgeError? Refusal(BridgeCommand command, JsonObject? parameters, PythonAccess access, ScriptAdvice advice)
    {
        if (access == PythonAccess.Allow)
        {
            return null;
        }

        var isScriptOperator = command.Name == BridgeCommands.RunOperator.Name
            && RunsScripts(parameters?["name"]?.GetValue<string>() ?? string.Empty, parameters?["params"] as JsonObject);

        if (access == PythonAccess.Off)
        {
            return command.Name == BridgeCommands.Python.Name || isScriptOperator
                ? new BridgeError("PythonDisabled", $"'{command.Name}' is switched off by configuration on this server.")
                : null;
        }

        if (command.Name != BridgeCommands.Python.Name)
        {
            return null;
        }

        var covered = advice.Read(parameters?["code"]?.GetValue<string>());

        return covered.Count >= CoveredLimit
            ? new BridgeError("PythonCovered", $"the script does {covered.Count} things the tools already do: {string.Join(", ", covered.Select(finding => finding.Tool))}")
            : null;
    }
}
