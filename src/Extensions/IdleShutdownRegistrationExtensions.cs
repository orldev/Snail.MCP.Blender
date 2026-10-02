using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Snail.MCP.Blender.Configuration;
using Snail.MCP.Blender.Hosting;

namespace Snail.MCP.Blender.Extensions;

/// <summary>Host lifetime: the idle watchdog that exits an orphaned server process.</summary>
public static class IdleShutdownRegistrationExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddIdleShutdownWatcher()
        {
            services.TryAddSingleton(TimeProvider.System);
            services.AddSingleton<IActivityTracker, ActivityTracker>();
            services.AddSingleton(provider => IdleShutdownOptions.For(provider.GetRequiredService<ServerConfig>().IdleTimeoutMinutes));
            services.AddHostedService<IdleShutdownWatcher>();

            return services;
        }
    }
}
