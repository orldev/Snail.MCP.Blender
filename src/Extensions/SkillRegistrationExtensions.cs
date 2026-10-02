using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Server;
using Snail.MCP.Blender.Application.Access;
using Snail.MCP.Blender.Application.Discovery;
using Snail.MCP.Blender.Application.Rendering;
using Snail.MCP.Blender.Application.Sessions;
using Snail.MCP.Blender.Application.Skills;
using Snail.MCP.Blender.Configuration;

namespace Snail.MCP.Blender.Extensions;

/// <summary>Skills: the on-demand tool groups, their catalog and the watchdog that unloads idle ones.</summary>
public static class SkillRegistrationExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddSkills()
        {
            services.TryAddSingleton(TimeProvider.System);
            services.AddSingleton(SkillCatalog.Discover(typeof(SkillAttribute).Assembly));
            services.AddSingleton(provider => new ClientSessions(
                provider.GetRequiredService<IReadOnlyList<Skill>>(),
                provider.GetRequiredService<IOptions<McpServerOptions>>().Value.ToolCollection ??= [],
                provider.GetRequiredService<ServerConfig>().Bridge.Agent,
                provider,
                provider.GetRequiredService<TimeProvider>()));
            services.AddTransient(provider => provider.GetRequiredService<ClientSessions>().Current.Skills);
            services.AddSingleton(new ToolIndex(typeof(SkillAttribute).Assembly));
            services.AddSingleton<CommandSemantics>();
            services.AddSingleton<ToolScopes>();
            services.AddSingleton<ScriptAdvice>();
            services.AddSingleton<RenderJobs>();
            services.AddSingleton(provider => SkillExpiryOptions.For(provider.GetRequiredService<ServerConfig>().SkillIdleMinutes));
            services.AddHostedService<SkillExpiry>();

            return services;
        }
    }

    extension(IMcpServerBuilder builder)
    {
        /// <summary>Every tool class of the assembly that belongs to no skill: the always-on layer.</summary>
        /// <remarks>The cast picks the overload that scans the listed types; without it the call binds to the generic
        /// overload that treats the list itself as a tool instance and registers nothing.</remarks>
        public IMcpServerBuilder WithBaseTools() =>
            builder.WithTools((IEnumerable<Type>)BaseToolTypes(typeof(SkillAttribute).Assembly));
    }

    public static IReadOnlyList<Type> BaseToolTypes(Assembly assembly) =>
        [.. assembly.GetTypes()
            .Where(type => type.GetCustomAttribute<McpServerToolTypeAttribute>() is not null)
            .Where(type => type.GetCustomAttribute<SkillAttribute>() is null)];
}
