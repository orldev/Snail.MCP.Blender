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
            builder.UseStderrLogging();
            builder.AddServerConfiguration();

            builder.Services
                .AddBlenderBridge()
                .AddSkills()
                .AddIdleShutdownWatcher()
                .AddMcpTransport();

            return builder;
        }

        /// <summary>stdio carries JSON-RPC, so every log line must go to stderr.</summary>
        internal HostApplicationBuilder UseStderrLogging()
        {
            builder.Logging.ClearProviders();
            builder.Logging.AddConsole(options => options.LogToStandardErrorThreshold = LogLevel.Trace);

            return builder;
        }
    }
}
