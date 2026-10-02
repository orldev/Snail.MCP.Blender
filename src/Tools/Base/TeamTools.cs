using System.ComponentModel;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Snail.MCP.Blender.Application.Access;
using Snail.MCP.Blender.Application.Discovery;
using Snail.MCP.Blender.Application.Sessions;
using Snail.MCP.Blender.Configuration;
using Snail.MCP.Blender.Tools.Prompts;

namespace Snail.MCP.Blender.Tools.Base;

/// <summary>Working alongside other agents in one Blender: the journal of who changed what, leases on objects, and batches that run elsewhere.</summary>
[McpServerToolType]
public sealed class TeamTools(IBlenderBridge bridge, ServerConfig config, ScriptAdvice advice, ToolScopes scopes, ClientSessions clients) : BridgeToolBase(bridge)
{
    [McpServerTool(Name = "blender_journal", ReadOnly = true, OpenWorld = false)]
    [Description(ToolDescriptions.Team.Journal)]
    public Task<CallToolResult> JournalAsync(
        [Description("Only entries after this index; the latest index comes back with every reply.")] int since = 0,
        [Description("Only this agent's entries.")] string? agent = null,
        [Description("Only entries that touched this object or collection.")] string? target = null,
        [Description("Most entries to return, up to 500.")] int limit = 100,
        CancellationToken cancellationToken = default) =>
        SendAsync(BridgeCommands.Journal, new JsonObject().With("since", since).With("author", agent).With("target", target).With("limit", limit), cancellationToken);

    [McpServerTool(Name = "blender_lease", Destructive = false, OpenWorld = false)]
    [Description(ToolDescriptions.Team.Lease)]
    public Task<CallToolResult> LeaseAsync(
        [Description("claim, release, list (the default) or clear.")] string action = "list",
        [Description("Objects or collections (every object inside) to claim or release.")] string[]? names = null,
        [Description("claim: seconds the lease lasts; 900 by default.")] int? ttlSeconds = null,
        [Description("claim: what the agent is doing with them.")] string? note = null,
        [Description("release: take leases held by other agents too.")] bool force = false,
        CancellationToken cancellationToken = default) =>
        SendAsync(BridgeCommands.Lease, new JsonObject().With("action", action).With("names", names).With("ttl_seconds", ttlSeconds).With("note", note).With("force", force), cancellationToken);

    [McpServerTool(Name = "blender_batch", Destructive = true, OpenWorld = true)]
    [Description(ToolDescriptions.Team.Batch)]
    public Task<CallToolResult> BatchAsync(
        [Description("Steps in order: [{\"command\": \"add_primitive\", \"params\": {\"kind\": \"cube\", \"name\": \"Crate\"}}, ...]; command names are the bridge names, the tool name without blender_.")] JsonArray commands,
        [Description("Absolute .blend to start from; a copy of the current file otherwise.")] string? input = null,
        [Description("Absolute .blend to save the result to; output.blend in the batch folder otherwise.")] string? output = null,
        [Description("Keep going after a failed step.")] bool continueOnError = false,
        [Description("Name for the batch.")] string? name = null,
        CancellationToken cancellationToken = default) =>
        (OperatorTools.RefusalFor(commands, scopes, clients.Current)
            ?? OperatorTools.RefusalFor(commands, config.Python, advice, Messages.BatchPythonDisabled)) is { } refused
            ? Task.FromResult(refused)
            : SendAsync(BridgeCommands.BatchJob,
                new JsonObject().With("commands", commands).With("input", input).With("output", output).With("continue_on_error", continueOnError).With("name", name),
                cancellationToken, TimeSpan.FromSeconds(120));

    [McpServerTool(Name = "blender_batch_status", ReadOnly = true, OpenWorld = false)]
    [Description(ToolDescriptions.Team.BatchStatus)]
    public Task<CallToolResult> BatchStatusAsync(
        [Description("Batch id as blender_batch returned it; every recent batch otherwise.")] string? id = null,
        [Description("Lines from the end of the batch log.")] int logLines = 10,
        CancellationToken cancellationToken = default) =>
        SendAsync(BridgeCommands.BatchStatus, new JsonObject().With("id", id).With("log_lines", logLines), cancellationToken);
}
