using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Server;
using Snail.MCP.Blender.Application.Access;
using Snail.MCP.Blender.Application.Discovery;
using Snail.MCP.Blender.Application.Sessions;

namespace Snail.MCP.Blender.Tests.Support;

/// <summary>The scope rule and the clients a test serves, built the way the server builds them.</summary>
/// <remarks>The rule reads the command schema bundled next to the test binary, so a test asks the same question of the same file the server
/// does rather than a table of its own.</remarks>
public static class Access
{
    public static ToolScopes Scopes { get; } = new(new CommandSemantics());

    /// <summary>Sessions whose current client is the server itself, which may do anything: what a tool test wants unless it is about scopes.</summary>
    public static ClientSessions Sessions() =>
        new([], [], agent: null, new ServiceCollection().BuildServiceProvider(), TimeProvider.System);

    /// <summary>Serves a client of exactly these scopes until the returned handle is disposed.</summary>
    public static (ClientSessions Clients, IDisposable Serving) Serving(string name, params string[] scopes)
    {
        var clients = Sessions();
        var client = clients.Open(new McpServerOptions(), name, scopes);

        return (clients, clients.Serve(client));
    }
}
