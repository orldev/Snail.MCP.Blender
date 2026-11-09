using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Server;
using Snail.MCP.Blender.Application.Access;
using Snail.MCP.Blender.Configuration;
using Snail.Sdk.Mcp.Hosting.Sessions;

namespace Snail.MCP.Blender.Tests.Extensions;

/// <summary>The composition root wired in-process: every tool, the skills' included, lands in the server's one tool collection beside the tools
/// Snail.Sdk adds, and the gate the SDK asks is the server's own.</summary>
public class CompositionTests
{
    [Fact]
    public void Composition_EveryTool_LandsInTheServerToolCollection()
    {
        var provider = Build();

        var options = provider.GetRequiredService<IOptions<McpServerOptions>>().Value;
        var declared = typeof(ServerConfig).Assembly.GetTypes()
            .Where(type => type.GetCustomAttribute<McpServerToolTypeAttribute>() is not null)
            .SelectMany(type => type.GetMethods())
            .Select(method => method.GetCustomAttribute<McpServerToolAttribute>()?.Name)
            .OfType<string>();
        var expected = declared.Concat(ServerTools.OfTheSdk).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();

        Assert.Equal(expected, options.ToolCollection!.PrimitiveNames.Order(StringComparer.Ordinal));
    }

    /// <summary>The door, the tool list, run_program and the server's carriers ask one gate: the scopes of the client's key, not the
    /// <c>?tool=</c> of an address this server never reads.</summary>
    [Fact]
    public void Composition_TheGateTheSdkAsks_IsTheScopeGate()
    {
        var provider = Build();

        Assert.IsType<ScopeGate>(provider.GetRequiredService<IToolGate>());
    }

    private static ServiceProvider Build() => Access.Server();
}
