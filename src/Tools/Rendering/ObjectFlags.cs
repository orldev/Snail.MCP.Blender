namespace Snail.MCP.Blender.Tools.Rendering;

/// <summary>Which ray types see an object.</summary>
public sealed record RayVisibility : Block
{
    public bool? Camera { get; init; }

    public bool? Diffuse { get; init; }

    public bool? Glossy { get; init; }

    public bool? Transmission { get; init; }

    public bool? VolumeScatter { get; init; }

    public bool? Shadow { get; init; }
}

/// <summary>An object's role in caustics.</summary>
public sealed record CausticsRole : Block
{
    public bool? Caster { get; init; }

    public bool? Receiver { get; init; }
}
