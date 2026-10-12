using Microsoft.Extensions.Hosting;
using Snail.MCP.Blender.Configuration;

namespace Snail.MCP.Blender.Extensions;

/// <summary>Composition root: lists the features to wire; how sockets or the transport are configured is known one level down.</summary>
public static class McpServerRegistrationExtensions
{
    extension(HostApplicationBuilder builder)
    {
        public HostApplicationBuilder ConfigureMcpServer()
        {
            builder.AddServerConfiguration();

            builder.Services
                .AddBlenderBridge()
                .AddSkills()
                .AddIdleShutdownWatcher()
                .AddMcpTransport();

            return builder;
        }
    }
}
