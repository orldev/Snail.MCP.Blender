using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Server;
using Snail.MCP.Blender.Application.Sessions;
using Snail.Sdk.Mcp.Hosting.Sessions;

namespace Snail.MCP.Blender.Application.Access;

/// <summary>Whether the client in hand may call a tool: the kind of work it is (<see cref="ToolScopes"/>) against what the client's key opens.</summary>
/// <remarks>The one gate of the server. Snail.Sdk asks it at the door for every call and for the tool list, before every command of
/// <c>run_program</c>, and the server's own carriers — <c>run_tools</c>, <c>blender_batch</c> — ask it for every command they carry, under the
/// name of the tool the command stands for (<see cref="BridgeCommands.ToolOf"/>). A tool the server registered is judged by the tool, its
/// annotations included; a command no tool sends, by the command's own meaning.</remarks>
public sealed class ScopeGate(ToolScopes scopes, ClientSessions clients, IServiceProvider root) : IToolGate
{
    private IReadOnlyDictionary<string, McpServerTool>? _tools;

    /// <inheritdoc />
    public string? Refusal(string tool, IServiceProvider services)
    {
        var client = clients.Current;
        var scope = ScopeOf(tool);

        return client.May(scope) ? null : ToolScopes.Refusal(tool, scope, client.Name);
    }

    /// <summary>The scope a call of this tool needs: the registered tool's, or the command's when no tool of the name is registered.</summary>
    public string ScopeOf(string tool)
    {
        if (Registered().TryGetValue(tool, out var registered))
        {
            return scopes.For(registered);
        }

        var command = tool.StartsWith(BridgeCommands.ToolPrefix, StringComparison.Ordinal) ? tool[BridgeCommands.ToolPrefix.Length..] : tool;

        return scopes.For(command);
    }

    /// <summary>The tools the server registered, by name, read once the server's options are built: the gate is asked only after that.</summary>
    private IReadOnlyDictionary<string, McpServerTool> Registered() =>
        _tools ??= (root.GetService<IOptions<McpServerOptions>>()?.Value.ToolCollection ?? [])
            .ToDictionary(tool => tool.ProtocolTool.Name, StringComparer.Ordinal);
}
