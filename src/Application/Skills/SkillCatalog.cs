using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Server;

namespace Snail.MCP.Blender.Application.Skills;

/// <summary>Loads and unloads skills into the server's tool collection; the SDK announces each change to the client as tools/list_changed.</summary>
/// <remarks>Whether a skill is enabled is read from the collection itself rather than kept beside it, so the catalog cannot
/// disagree with what the client was told. One catalog belongs to one client: a stdio process has the one its session holds, and over HTTP
/// each session opens its own, so the skills one client loads are that client's and the others keep the tool list they had.</remarks>
public sealed class SkillCatalog(
    IReadOnlyList<Skill> skills,
    McpServerPrimitiveCollection<McpServerTool> tools,
    IServiceProvider services,
    TimeProvider timeProvider)
{
    private readonly Dictionary<string, IReadOnlyList<McpServerTool>> _built = new(StringComparer.Ordinal);
    private readonly Dictionary<string, DateTimeOffset> _lastUsed = new(StringComparer.Ordinal);
    private readonly HashSet<string> _pinned = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Skill> _owners = OwnersOf(skills);
    private readonly Lock _gate = new();

    public IReadOnlyList<Skill> Skills { get; } = skills;

    /// <summary>The collection this catalog loads into: one client's tool list, which the SDK announces to that client alone.</summary>
    public McpServerPrimitiveCollection<McpServerTool> Tools => tools;

    public IReadOnlyList<SkillState> Status() => [.. Skills.Select(Describe)];

    public SkillState? Enable(string name)
    {
        if (Find(name) is not { } skill)
        {
            return null;
        }

        lock (_gate)
        {
            using var batch = tools.DeferChangedEvents();

            foreach (var tool in Build(skill))
            {
                tools.TryAdd(tool);
            }

            _lastUsed[skill.Name] = timeProvider.GetUtcNow();
        }

        return Describe(skill);
    }

    public SkillState? Disable(string name)
    {
        if (Find(name) is not { } skill)
        {
            return null;
        }

        lock (_gate)
        {
            using var batch = tools.DeferChangedEvents();

            foreach (var tool in Build(skill))
            {
                tools.Remove(tool);
            }

            _lastUsed.Remove(skill.Name);
        }

        return Describe(skill);
    }

    /// <summary>Marks the skill owning a tool as used, so idle expiry leaves it alone.</summary>
    public void Touch(string? toolName)
    {
        if (toolName is null || OwnerOf(toolName) is not { } skill)
        {
            return;
        }

        lock (_gate)
        {
            _lastUsed[skill.Name] = timeProvider.GetUtcNow();
        }
    }

    /// <summary>Keeps a skill loaded through idle expiry, for as long as something it started is still running.</summary>
    public void Pin(string name)
    {
        lock (_gate)
        {
            _pinned.Add(name);
        }
    }

    public void Unpin(string name)
    {
        lock (_gate)
        {
            _pinned.Remove(name);
        }
    }

    /// <summary>Unloads every enabled, unpinned skill that has not been used for longer than <paramref name="idle"/>; returns their names.</summary>
    public IReadOnlyList<string> ExpireIdle(TimeSpan idle)
    {
        var now = timeProvider.GetUtcNow();
        var expired = Skills
            .Where(skill => IsEnabled(skill) && !IsPinned(skill) && now - LastUsed(skill) >= idle)
            .Select(skill => skill.Name)
            .ToList();

        foreach (var name in expired)
        {
            Disable(name);
        }

        return expired;
    }

    private Skill? Find(string name) =>
        Skills.FirstOrDefault(skill => string.Equals(skill.Name, name, StringComparison.OrdinalIgnoreCase));

    private Skill? OwnerOf(string toolName) => _owners.GetValueOrDefault(toolName);

    /// <summary>Tool name to skill, read off the attributes once; the tool objects themselves are built only when a skill is enabled.</summary>
    private static Dictionary<string, Skill> OwnersOf(IReadOnlyList<Skill> skills) =>
        skills
            .SelectMany(skill => skill.ToolTypes
                .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                .Select(method => method.GetCustomAttribute<McpServerToolAttribute>()?.Name)
                .OfType<string>()
                .Select(name => (Name: name, Skill: skill)))
            .ToDictionary(pair => pair.Name, pair => pair.Skill, StringComparer.Ordinal);

    private DateTimeOffset LastUsed(Skill skill)
    {
        lock (_gate)
        {
            return _lastUsed.TryGetValue(skill.Name, out var used) ? used : DateTimeOffset.MinValue;
        }
    }

    private bool IsEnabled(Skill skill) => Build(skill).All(tools.Contains);

    private bool IsPinned(Skill skill)
    {
        lock (_gate)
        {
            return _pinned.Contains(skill.Name);
        }
    }

    private SkillState Describe(Skill skill) =>
        new(skill.Name, skill.Description, IsEnabled(skill), [.. Build(skill).Select(tool => tool.ProtocolTool.Name)]);

    private IReadOnlyList<McpServerTool> Build(Skill skill)
    {
        lock (_gate)
        {
            if (_built.TryGetValue(skill.Name, out var built))
            {
                return built;
            }

            built = [.. skill.ToolTypes.SelectMany(ToolsOf)];
            _built[skill.Name] = built;

            return built;
        }
    }

    private IEnumerable<McpServerTool> ToolsOf(Type type) =>
        type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(method => method.GetCustomAttribute<McpServerToolAttribute>() is not null)
            .Select(method => McpServerTool.Create(
                method,
                context => ActivatorUtilities.CreateInstance(context.Services ?? services, type),
                new McpServerToolCreateOptions { Services = services }));

    /// <summary>Skills declared in an assembly: every tool class carrying <see cref="SkillAttribute"/>, grouped by skill name.</summary>
    public static IReadOnlyList<Skill> Discover(Assembly assembly) =>
        [.. assembly.GetTypes()
            .Select(type => (Type: type, Skill: type.GetCustomAttribute<SkillAttribute>()))
            .Where(pair => pair.Skill is not null)
            .GroupBy(pair => pair.Skill!.Name, StringComparer.Ordinal)
            .Select(group => new Skill(group.Key, group.First().Skill!.Description, [.. group.Select(pair => pair.Type)]))
            .OrderBy(skill => skill.Name, StringComparer.Ordinal)];
}
