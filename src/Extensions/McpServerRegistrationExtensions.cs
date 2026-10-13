using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Snail.MCP.Blender.Configuration;

namespace Snail.MCP.Blender.Extensions;

/// <summary>Composition root: lists the features to wire; how sockets or the transport are configured is known one level down.</summary>
public static class McpServerRegistrationExtensions
{
    extension(HostApplicationBuilder builder)
    {
        /// <summary>Binds the server to <paramref name="settings"/>, the ones the transport was chosen from, or to this machine's when none are given.</summary>
        public HostApplicationBuilder ConfigureMcpServer(IConfiguration? settings = null)
        {
            builder.AddServerConfiguration(settings ?? ServerConfigExtensions.Settings());

            builder.Services
                .AddBlenderBridge()
                .AddSkills()
                .AddIdleShutdownWatcher()
                .AddMcpTransport();

            return builder;
        }
    }
}
