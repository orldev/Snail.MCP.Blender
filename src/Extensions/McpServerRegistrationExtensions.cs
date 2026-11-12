using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Snail.MCP.Blender.Configuration;
using Snail.Sdk.Mcp.Hosting;
using Snail.Sdk.Mcp.Hosting.Jobs;

namespace Snail.MCP.Blender.Extensions;

/// <summary>Composition root: lists the features to wire; how sockets or the transport are configured is known one level down.</summary>
internal static class McpServerRegistrationExtensions
{
    extension(HostApplicationBuilder builder)
    {
        /// <summary>Binds the server to <paramref name="settings"/>, the ones the transport was chosen from, or to this machine's when none are given.</summary>
        public HostApplicationBuilder ConfigureMcpServer(IConfiguration? settings = null)
        {
            settings ??= ServerConfigExtensions.Settings();

            var config = ServerConfigExtensions.Peek(settings);

            builder.AddServerConfiguration(settings);

            builder.Services
                .AddBlenderBridge()
                .AddAccess()
                .AddIdleShutdown(TimeSpan.FromMinutes(config.IdleTimeoutMinutes), IsRendering)
                .AddMcpTransport(config);

            return builder;
        }
    }

    /// <summary>Whether a render or a batch this server follows is still running: a client that waits for its frames sends nothing meanwhile,
    /// and the idle shutdown would otherwise stop the server under it.</summary>
    private static bool IsRendering(IServiceProvider services) => services.GetService<Jobs>()?.InFlight.Count > 0;
}
