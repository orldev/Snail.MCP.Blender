using Snail.MCP.Blender.Application.Discovery;
using Snail.MCP.Blender.Configuration;
using Snail.MCP.Blender.Tools.Prompts;
using Snail.Sdk.Mcp.Programs;

namespace Snail.MCP.Blender.Tools;

/// <summary>The commands a program of <c>run_program</c> calls on its global object: every command of the add-on's catalog, <c>blender.add_primitive({ … })</c>.</summary>
/// <remarks>
/// <para>
/// Snail.Sdk runs the program and asks the session's gate before every command, under the tool the command stands for
/// (<see cref="BridgeCommands.ToolOf"/>): a key issued without the Python scope does not run Python by writing one line of JavaScript.
/// The Python setting is this catalog's own and holds here, the rule blender_python follows: off refuses the script, fallback refuses a
/// script whose work the tools already do.
/// </para>
/// <para>
/// A failed command reaches the program as <c>{ error, message, hint, type, details }</c> with the add-on's own type — <c>Leased</c>,
/// <c>NotFound</c> — so the program may catch it and carry on. The traffic report counts the program once it has run
/// (<c>ProgramOptions.Ran</c>), not here.
/// </para>
/// </remarks>
internal sealed class ProgramCommands(IBlenderBridge bridge, ServerConfig config, ScriptAdvice advice) : IProgramCommands
{
    /// <inheritdoc />
    public IReadOnlyList<string> Names { get; } = [.. BridgeCommands.All.Select(command => command.Name)];

    /// <inheritdoc />
    public string ToolOf(string command) => BridgeCommands.ToolOf(command);

    /// <inheritdoc />
    /// <remarks>A command waits as long as its program has left (<see cref="CommandCall.Remaining"/>), not the single reply's
    /// <see cref="BlenderLinkOptions.RequestTimeoutSeconds"/>: <c>blender.render_image()</c> was cut off at 30 seconds inside a program given minutes.</remarks>
    public async Task<Answer<JsonNode?>> CallAsync(CommandCall call, CancellationToken cancellationToken)
    {
        var known = BridgeCommands.All.First(each => string.Equals(each.Name, call.Command, StringComparison.Ordinal));

        if (ScriptAccess.Refusal(known, call.Arguments, config.Python, advice) is { } refused)
        {
            return Failed(refused);
        }

        var reply = await bridge.SendAsync(known, call.Arguments, call.Remaining, cancellationToken).ConfigureAwait(false);

        return reply.IsOk ? Answer.Ok<JsonNode?>(reply.Result ?? new JsonObject()) : Failed(reply.Error!);
    }

    private static Answer<JsonNode?> Failed(BridgeError error) =>
        Answer.Failed(error.Message, Messages.HintFor(error), error.Type, error.Details?.DeepClone());
}
