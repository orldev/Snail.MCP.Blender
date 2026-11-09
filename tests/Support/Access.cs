using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Server;
using Snail.MCP.Blender.Application.Access;
using Snail.MCP.Blender.Application.Discovery;
using Snail.MCP.Blender.Application.Sessions;
using Snail.MCP.Blender.Configuration;
using Snail.MCP.Blender.Extensions;
using Snail.Sdk.Mcp.Hosting.Discovery;
using Snail.Sdk.Mcp.Hosting.Sessions;

namespace Snail.MCP.Blender.Tests.Support;

/// <summary>The scope rule, the gate and the clients a test serves, and the tools discovery reads, built the way the server builds them.</summary>
/// <remarks>The rule reads the command schema bundled next to the test binary, so a test asks the same question of the same file the server
/// does rather than a table of its own.</remarks>
public static class Access
{
    public static ToolScopes Scopes { get; } = new(new CommandSemantics());

    /// <summary>The services a tool test hands a tool that asks the gate: none, as the gate reads the client from its sessions.</summary>
    public static IServiceProvider Services { get; } = new ServiceCollection().BuildServiceProvider();

    /// <summary>Every tool of the server, skills included, as Snail.Sdk's discovery reads them.</summary>
    public static IReadOnlyList<ToolEntry> Catalog { get; } =
    [
        .. typeof(ServerConfig).Assembly.GetTypes()
            .Where(type => type.GetCustomAttribute<McpServerToolTypeAttribute>() is not null)
            .SelectMany(type => ToolCatalog.Of(type)),
    ];

    /// <summary>Script advice over every tool of the server.</summary>
    public static ScriptAdvice Advice() => new(Catalog);

    /// <summary>Sessions whose current client is the server itself, which may do anything: what a tool test wants unless it is about scopes.</summary>
    public static ClientSessions Sessions() => new(agent: null);

    /// <summary>The gate over these clients, or over the server's own client, which may do anything.</summary>
    public static ScopeGate Gate(ClientSessions? clients = null) => new(Scopes, clients ?? Sessions(), Services);

    /// <summary>The services a tool that carries commands is served with: the gate over these clients — by <paramref name="scopes"/>, or the
    /// server's rule — and the grant's options as the server registers them, so a refusal reads as Snail.Sdk's <c>ToolForwarding</c> gives it.</summary>
    public static IServiceProvider Served(ClientSessions? clients = null, ToolScopes? scopes = null)
    {
        var grant = new GrantOptions();
        TransportRegistrationExtensions.Granting(grant);
        var services = new ServiceCollection();
        services.AddSingleton(grant);
        services.AddSingleton<IToolGate>(provider => new ScopeGate(scopes ?? Scopes, clients ?? Sessions(), provider));

        return services.BuildServiceProvider();
    }

    /// <summary>Serves a client of exactly these scopes until the returned handle is disposed.</summary>
    public static (ClientSessions Clients, IDisposable Serving) Serving(string name, params string[] scopes)
    {
        var clients = Sessions();

        return (clients, clients.Serve(clientName: null, name, scopes));
    }

    /// <summary>The server's services as its composition root registers them, with no host: what <c>ToolCatalog.Find(services, …)</c> reads,
    /// so a test of <c>find_tool</c>'s answer asks the catalog, the commands and the parameter names <c>find_tool</c> itself answers with.</summary>
    public static ServiceProvider Server()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(new ServerConfig());
        services.AddSingleton(provider => provider.GetRequiredService<ServerConfig>().Bridge);
        services.AddBlenderBridge().AddAccess().AddMcpTransport(new ServerConfig());

        return services.BuildServiceProvider();
    }

    /// <summary>The options <c>find_tool</c> is registered with.</summary>
    public static DiscoveryOptions Discovery()
    {
        var options = new DiscoveryOptions();

        TransportRegistrationExtensions.Discovering(options);

        return options;
    }
}
