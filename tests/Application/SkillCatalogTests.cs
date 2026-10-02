using System.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using ModelContextProtocol.Server;
using Snail.MCP.Blender.Application.Skills;

namespace Snail.MCP.Blender.Tests.Application;

public class SkillCatalogTests
{
    private static readonly TimeSpan Idle = TimeSpan.FromMinutes(30);

    [Fact]
    public void Enable_KnownSkill_AddsItsToolsAndAnnouncesTheChangeOnce()
    {
        var (catalog, tools, _) = Build();
        var changes = 0;
        tools.Changed += (_, _) => changes++;

        var state = catalog.Enable("probe");

        Assert.True(state!.IsEnabled);
        Assert.Equal(["blender_probe_one", "blender_probe_two"], state.Tools);
        Assert.Equal(["blender_probe_one", "blender_probe_two"], tools.PrimitiveNames.Order());
        Assert.Equal(1, changes);
    }

    [Fact]
    public void Disable_EnabledSkill_RemovesItsTools()
    {
        var (catalog, tools, _) = Build();
        catalog.Enable("probe");

        var state = catalog.Disable("Probe");

        Assert.False(state!.IsEnabled);
        Assert.Empty(tools.PrimitiveNames);
    }

    [Fact]
    public void Enable_UnknownSkill_AnswersNull()
    {
        var (catalog, _, _) = Build();

        Assert.Null(catalog.Enable("sculpting"));
    }

    [Fact]
    public void Status_ReadsEnablementFromTheCollection()
    {
        var (catalog, tools, _) = Build();
        catalog.Enable("probe");
        tools.Remove(tools["blender_probe_one"]);

        var state = catalog.Status().Single(skill => skill.Name == "probe");

        Assert.False(state.IsEnabled);
    }

    [Fact]
    public void ExpireIdle_UnusedSkill_IsUnloaded_WhileATouchedOneStays()
    {
        var (catalog, tools, time) = Build();
        catalog.Enable("probe");
        catalog.Enable("other");
        time.Advance(Idle);
        catalog.Touch("blender_other_tool");
        time.Advance(TimeSpan.FromMinutes(1));

        var expired = catalog.ExpireIdle(Idle);

        Assert.Equal(["probe"], expired);
        Assert.Equal(["blender_other_tool"], tools.PrimitiveNames);
    }

    [Fact]
    public void ExpireIdle_PinnedSkill_StaysLoaded_UntilUnpinned()
    {
        var (catalog, tools, time) = Build();
        catalog.Enable("probe");
        catalog.Pin("probe");
        time.Advance(Idle + TimeSpan.FromMinutes(1));

        Assert.Empty(catalog.ExpireIdle(Idle));
        Assert.Equal(2, tools.PrimitiveNames.Count);

        catalog.Unpin("probe");

        Assert.Equal(["probe"], catalog.ExpireIdle(Idle));
    }

    [Fact]
    public void Discover_ThisAssembly_GroupsSkillClassesByName()
    {
        var skills = SkillCatalog.Discover(typeof(SkillCatalogTests).Assembly);

        Assert.Equal(["other", "probe", "sessions"], skills.Select(skill => skill.Name));
        Assert.Equal([typeof(ProbeTools)], skills[1].ToolTypes);
    }

    private static (SkillCatalog Catalog, McpServerPrimitiveCollection<McpServerTool> Tools, FakeTimeProvider Time) Build()
    {
        var time = new FakeTimeProvider();
        var tools = new McpServerPrimitiveCollection<McpServerTool>();
        var services = new ServiceCollection().AddSingleton(new Marker("probe-marker")).BuildServiceProvider();
        var skills = SkillCatalog.Discover(typeof(SkillCatalogTests).Assembly);

        return (new SkillCatalog(skills, tools, services, time), tools, time);
    }

    public sealed record Marker(string Text);

    [McpServerToolType]
    [Skill("probe", "A probe skill.")]
    public sealed class ProbeTools
    {
        [McpServerTool(Name = "blender_probe_one")]
        [Description("one")]
        public string One() => "one";

        [McpServerTool(Name = "blender_probe_two")]
        [Description("two")]
        public string Two() => "two";
    }

    [McpServerToolType]
    [Skill("other", "Another skill.")]
    public sealed class OtherTools(Marker marker)
    {
        [McpServerTool(Name = "blender_other_tool")]
        [Description("other")]
        public string Other() => marker.Text;
    }
}
