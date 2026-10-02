namespace Snail.MCP.Blender.Application.Skills;

/// <summary>Marks a tool class as part of a skill: its tools stay out of the catalog until the skill is enabled.</summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class SkillAttribute(string name, string description) : Attribute
{
    public string Name { get; } = name;

    public string Description { get; } = description;
}
