using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Server;
using Snail.MCP.Blender.Application.Skills;
using Snail.MCP.Blender.Configuration;
using Snail.MCP.Blender.Extensions;
using Snail.MCP.Blender.Tools.Prompts;

namespace Snail.MCP.Blender.Tests.Extensions;

/// <summary>The composition root wired in-process: the base tools land in the server's tool collection and the skill catalog shares that same collection.</summary>
public class CompositionTests
{
    [Fact]
    public void Composition_BaseTools_LandInTheServerToolCollection()
    {
        var provider = Build();

        var options = provider.GetRequiredService<IOptions<McpServerOptions>>().Value;
        var expected = SkillRegistrationExtensions.BaseToolTypes(typeof(ToolDescriptions).Assembly)
            .SelectMany(type => type.GetMethods())
            .Select(method => method.GetCustomAttributes(typeof(McpServerToolAttribute), false).OfType<McpServerToolAttribute>().FirstOrDefault()?.Name)
            .OfType<string>()
            .Order()
            .ToList();

        Assert.Equal(expected, options.ToolCollection!.PrimitiveNames.Order());
    }

    [Fact]
    public void Composition_SkillCatalog_SharesTheServerToolCollection()
    {
        var provider = Build();

        var options = provider.GetRequiredService<IOptions<McpServerOptions>>().Value;
        var catalog = provider.GetRequiredService<SkillCatalog>();
        var before = options.ToolCollection!.Count;

        foreach (var skill in catalog.Skills)
        {
            catalog.Enable(skill.Name);
        }

        Assert.Equal(before + catalog.Skills.Sum(skill => catalog.Status().Single(state => state.Name == skill.Name).Tools.Count), options.ToolCollection.Count);
    }

    private static ServiceProvider Build()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(new ServerConfig());
        services.AddSingleton(provider => provider.GetRequiredService<ServerConfig>().Bridge);
        services.AddBlenderBridge().AddSkills().AddIdleShutdownWatcher().AddMcpTransport();

        return services.BuildServiceProvider();
    }
}
