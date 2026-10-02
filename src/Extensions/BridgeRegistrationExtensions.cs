using System.Diagnostics.Metrics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Snail.MCP.Blender.Adapters.AddOn;
using Snail.MCP.Blender.Adapters.Blender;
using Snail.MCP.Blender.Adapters.Programs;
using Snail.MCP.Blender.Adapters.Ssh;
using Snail.MCP.Blender.Application.Access;
using Snail.MCP.Blender.Application.Diagnostics;
using Snail.MCP.Blender.Application.Rendering;
using Snail.MCP.Blender.Application.Sessions;
using Snail.MCP.Blender.Application.Transfer;

namespace Snail.MCP.Blender.Extensions;

/// <summary>The link to Blender: the TCP client of the add-on behind a traffic trace, the SSH tunnel when Blender is elsewhere, the add-on packager and the health report over them.</summary>
public static class BridgeRegistrationExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddBlenderBridge()
        {
            services.AddSingleton<BlenderLinks>();
            services.AddSingleton(provider => new BridgeTraffic(provider.GetService<IMeterFactory>()));
            services.AddSingleton<IBlenderBridge>(provider =>
                new TracedBridge(
                    provider.GetRequiredService<BlenderLinks>(),
                    provider.GetRequiredService<BridgeTraffic>(),
                    provider.GetRequiredService<ILogger<TracedBridge>>(),
                    provider.GetRequiredService<ClientSessions>()));
            services.AddSingleton<IBlenderMonitor, BlenderMonitor>();
            services.AddSingleton<RenderWatch>();
            services.AddSingleton<RenderGallery>();
            services.AddSingleton<IProgramRunner, JavaScriptPrograms>();
            services.AddSingleton<IAddOnPackager, AddOnPackager>();
            services.AddSingleton<AddOnFreshness>();
            services.AddSingleton<ServerHealth>();
            services.AddSingleton<FileTransfer>();
            services.AddSingleton<ClientToken>();
            services.AddSingleton<ClientKeys>();
            services.AddSingleton<FileLinks>();
            services.AddSingleton<ClientCommands>();
            services.AddSingleton<LocalProject>();
            services.TryAddSingleton(TimeProvider.System);
            services.AddSingleton<ISshProcessLauncher, SshProcessLauncher>();
            services.AddSingleton<SshTunnel>();
            services.AddSingleton<ITunnel>(provider => provider.GetRequiredService<SshTunnel>());
            services.AddHostedService(provider => provider.GetRequiredService<SshTunnel>());

            return services;
        }
    }
}
