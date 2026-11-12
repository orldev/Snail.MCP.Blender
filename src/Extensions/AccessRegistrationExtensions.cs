using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Server;
using Snail.MCP.Blender.Application.Access;
using Snail.MCP.Blender.Application.Discovery;
using Snail.MCP.Blender.Application.Rendering;
using Snail.MCP.Blender.Application.Sessions;
using Snail.MCP.Blender.Configuration;
using Snail.Sdk.Mcp.Hosting.Discovery;
using Snail.Sdk.Mcp.Hosting.Sessions;

namespace Snail.MCP.Blender.Extensions;

/// <summary>Who may do what: the client in hand, the kind of work each call is, the gate Snail.Sdk asks, and the advice a script gets.</summary>
internal static class AccessRegistrationExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>The client of each request, the scope rule over the command schema, the gate over both, the script advice over the tools the
        /// server registers, and the add-on's jobs.</summary>
        /// <remarks>The gate is registered here rather than left to <c>WithSessionGrant</c>, whose own gate reads <c>?tool=</c> from the
        /// address: this server grants by the scopes of the key a client presents, and the door, the tool list, <c>run_program</c> and the
        /// server's carriers all ask the one gate.</remarks>
        public IServiceCollection AddAccess()
        {
            services.TryAddSingleton(TimeProvider.System);
            services.AddSingleton(provider => new ClientSessions(provider.GetRequiredService<ServerConfig>().Bridge.Agent));
            services.AddSingleton<CommandSemantics>();
            services.AddSingleton<ToolScopes>();
            services.AddSingleton<ScopeGate>();
            services.AddSingleton<IToolGate>(provider => provider.GetRequiredService<ScopeGate>());
            services.AddSingleton(provider => new ScriptAdvice(ToolCatalog.Of(Registered(provider), BridgeCommands.CommandOf)));
            services.AddSingleton<AddOnJobs>();

            return services;
        }
    }

    /// <summary>The tools the server registers, skills included, as the MCP SDK collected them.</summary>
    private static IEnumerable<McpServerTool> Registered(IServiceProvider provider) =>
        provider.GetService<IOptions<McpServerOptions>>()?.Value.ToolCollection ?? [];
}
