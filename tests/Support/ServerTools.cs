using System.Reflection;
using ModelContextProtocol.Server;
using Snail.MCP.Blender.Configuration;
using Snail.Sdk.Mcp.Hosting.Discovery;

namespace Snail.MCP.Blender.Tests.Support;

/// <summary>The tools a session is listed from its start: those of the server's classes that belong to no skill, and the ones Snail.Sdk adds.</summary>
public static class ServerTools
{
    /// <summary>What Snail.Sdk registers for this server: <c>find_tool</c>, <c>run_program</c> and the three skill tools.</summary>
    public static IReadOnlyList<string> OfTheSdk { get; } = ["find_tool", "run_program", "blender_skills", "blender_enable_skill", "blender_disable_skill"];

    /// <summary>The tool classes of the server that belong to no skill.</summary>
    public static IReadOnlyList<Type> BaseTypes() =>
    [
        .. typeof(ServerConfig).Assembly.GetTypes()
            .Where(type => type.GetCustomAttribute<McpServerToolTypeAttribute>() is not null)
            .Where(type => type.GetCustomAttribute<SkillAttribute>() is null),
    ];

    /// <summary>Every tool listed to a session that has loaded no skill.</summary>
    public static HashSet<string> BaseNames() =>
    [
        .. BaseTypes()
            .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            .Select(method => method.GetCustomAttribute<McpServerToolAttribute>()?.Name)
            .OfType<string>(),
        .. OfTheSdk,
    ];
}
