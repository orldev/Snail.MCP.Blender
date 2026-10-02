using System.ComponentModel;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Snail.MCP.Blender.Application.Access;
using Snail.MCP.Blender.Application.Discovery;
using Snail.MCP.Blender.Application.Sessions;
using Snail.MCP.Blender.Configuration;
using Snail.MCP.Blender.Tools.Prompts;

namespace Snail.MCP.Blender.Tools.Base;

/// <summary>Many commands in one call, run in the Blender that is already open: what a build of hundreds of steps costs instead of hundreds of round trips.</summary>
[McpServerToolType]
public sealed class SequenceTools(IBlenderBridge bridge, ServerConfig config, ScriptAdvice advice, ToolScopes scopes, ClientSessions clients) : BridgeToolBase(bridge)
{
    [McpServerTool(Name = "blender_run", Destructive = true, OpenWorld = true)]
    [Description(ToolDescriptions.Sequence.Run)]
    public Task<CallToolResult> RunAsync(
        [Description("Steps in order: [{\"command\": \"add_primitive\", \"params\": {\"kind\": \"cube\", \"name\": \"Crate\"}}, ...]; command names are the bridge names, the tool name without blender_. Up to 500 steps.")] JsonArray commands,
        [Description("Keep going after a failed step; either way the reply lists every step that ran with its result or its error.")] bool continueOnError = false,
        [Description(ToolDescriptions.Parameters.TimeoutSeconds)] int timeoutSeconds = 120,
        CancellationToken cancellationToken = default) =>
        (OperatorTools.RefusalFor(commands, scopes, clients.Current)
            ?? OperatorTools.RefusalFor(commands, config.Python, advice, Messages.SequencePythonDisabled)) is { } refused
            ? Task.FromResult(refused)
            : WithinAsync(timeoutSeconds, timeout => SendAsync(BridgeCommands.Run,
                new JsonObject().With("commands", commands).With("continue_on_error", continueOnError), cancellationToken, timeout));
}
