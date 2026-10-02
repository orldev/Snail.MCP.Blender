namespace Snail.MCP.Blender.Domain;

/// <summary>A group of tools loaded on demand, so the always-on catalog stays small enough for a model to choose from.</summary>
public sealed record Skill(string Name, string Description, IReadOnlyList<Type> ToolTypes);

/// <summary>What the model sees of a skill: whether it is loaded and which tools it brings.</summary>
public sealed record SkillState(string Name, string Description, bool IsEnabled, IReadOnlyList<string> Tools);

/// <summary>Names of the skills this server ships; each is a folder under Tools with classes marked by the skill attribute.</summary>
public static class Skills
{
    public const string Modeling = "modeling";

    public const string Materials = "materials";

    public const string Animation = "animation";

    public const string Rendering = "rendering";

    public const string Io = "io";

    public const string Physics = "physics";

    public const string Video = "video";

    public const string Post = "post";

    public const string Farm = "farm";
}
