namespace Snail.MCP.Blender.Tools.Prompts;

/// <summary>What <c>run_program</c> is for on this server, written ahead of the rules and limits Snail.Sdk states for every program.</summary>
/// <remarks>Outside <see cref="ToolDescriptions"/>, which holds the descriptions of this server's own tool methods: <c>run_program</c> is
/// Snail.Sdk's tool, and the SDK writes the rest of its description — how a failed command is thrown, the log, the limits.</remarks>
internal static class ProgramTexts
{
    public const string Purpose =
        "Runs a small JavaScript program that calls the commands itself: blender.add_primitive({kind: \"cube\", name: \"Crate\"}) and so on for " +
        "every command of the catalog — the names find_tool reports, the tool name without blender_ — with loops, conditions and the results " +
        "in hand. Use it when a build takes many steps, when each step depends on what the last one answered, or when only a summary of a long " +
        "listing matters: the steps run here, and only what the program returns comes back, instead of one call and one reply per step. This is " +
        "not Blender's Python: blender_python stays the last resort for what no command does.";
}
