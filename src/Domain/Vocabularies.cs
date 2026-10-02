namespace Snail.MCP.Blender.Domain;

/// <summary>Mesh primitives the add-on can add; the contract test keeps the list equal to <c>PRIMITIVES</c> in <c>addon/objects.py</c>.</summary>
public static class Primitives
{
    public static IReadOnlyList<string> Kinds { get; } =
        ["cube", "plane", "grid", "monkey", "circle", "uv_sphere", "ico_sphere", "cylinder", "cone", "torus"];
}

/// <summary>Light types Blender knows; the contract test keeps the list equal to <c>LIGHTS</c> in <c>addon/lights.py</c>.</summary>
public static class Lights
{
    public static IReadOnlyList<string> Kinds { get; } = ["POINT", "SUN", "SPOT", "AREA"];

    /// <summary>Area light shapes; the contract test keeps the list equal to <c>LIGHT_SHAPES</c> in <c>addon/lights.py</c>.</summary>
    public static IReadOnlyList<string> Shapes { get; } = ["SQUARE", "RECTANGLE", "DISK", "ELLIPSE"];
}

/// <summary>File formats the add-on imports and exports; the contract test keeps both lists equal to <c>IMPORTERS</c> and <c>EXPORTERS</c> in <c>addon/interchange.py</c>.</summary>
public static class FileFormats
{
    public static IReadOnlyList<string> Import { get; } =
        ["fbx", "obj", "stl", "ply", "gltf", "glb", "dae", "usd", "usda", "usdc", "usdz", "abc", "x3d", "svg", "dxf", "3ds"];

    public static IReadOnlyList<string> Export { get; } =
        ["fbx", "obj", "stl", "ply", "gltf", "glb", "dae", "usd", "usda", "usdc", "usdz", "abc", "x3d", "dxf", "3ds"];
}

/// <summary>Physics set-ups the add-on knows; the contract test keeps the list equal to <c>PHYSICS_KINDS</c> in <c>addon/physics.py</c>.</summary>
public static class PhysicsKinds
{
    public static IReadOnlyList<string> All { get; } =
        ["rigid_body", "rigid_body_passive", "cloth", "soft_body", "collision", "fluid_domain", "fluid_flow", "fluid_effector", "dynamic_paint"];
}

/// <summary>Empty display types; the contract test keeps the list equal to <c>EMPTY_TYPES</c> in <c>addon/objects.py</c>.</summary>
public static class EmptyTypes
{
    public static IReadOnlyList<string> All { get; } = ["PLAIN_AXES", "ARROWS", "SINGLE_ARROW", "CIRCLE", "CUBE", "SPHERE", "CONE", "IMAGE"];
}

/// <summary>Strip kinds, effect types, strip modifiers and ProRes profiles of the sequencer; the contract test keeps them equal to the tuples in <c>addon/sequencer.py</c>.</summary>
public static class Sequencer
{
    public static IReadOnlyList<string> StripKinds { get; } = ["movie", "image", "sound", "scene", "color", "text"];

    public static IReadOnlyList<string> EffectTypes { get; } =
    [
        "CROSS", "ADD", "SUBTRACT", "ALPHA_OVER", "ALPHA_UNDER", "GAMMA_CROSS", "COMPOSITOR", "MULTIPLY", "WIPE", "GLOW",
        "COLOR", "SPEED", "MULTICAM", "ADJUSTMENT", "GAUSSIAN_BLUR", "TEXT", "COLORMIX",
    ];

    public static IReadOnlyList<string> ModifierTypes { get; } =
        ["BRIGHT_CONTRAST", "COLOR_BALANCE", "CURVES", "HUE_CORRECT", "MASK", "TONEMAP", "WHITE_BALANCE", "SOUND_EQUALIZER", "PITCH", "ECHO"];

    public static IReadOnlyList<string> ProresProfiles { get; } = ["422_PROXY", "422_LT", "422_STD", "422_HQ", "4444", "4444_XQ"];
}

/// <summary>Colour depths and EXR codecs of image output; the contract test keeps them equal to <c>COLOR_DEPTHS</c> and <c>EXR_CODECS</c> in <c>addon/output.py</c>.</summary>
public static class Output
{
    public static IReadOnlyList<string> ColorDepths { get; } = ["8", "10", "12", "16", "32"];

    public static IReadOnlyList<string> ExrCodecs { get; } = ["NONE", "ZIP", "PIZ", "DWAA", "DWAB", "HTJ2K", "ZIPS", "RLE", "PXR24", "B44", "B44A"];

    /// <summary>Resolution presets; the contract test keeps the list equal to the keys of <c>RESOLUTION_PRESETS</c> in <c>addon/output.py</c>.</summary>
    public static IReadOnlyList<string> ResolutionPresets { get; } =
        ["HD", "FHD", "QHD", "UHD", "8K", "DCI_2K", "DCI_4K", "SQUARE_1K", "SQUARE_2K", "PORTRAIT_FHD", "PORTRAIT_UHD", "CINEMASCOPE_2K", "CINEMASCOPE_4K"];
}

/// <summary>Camera moves the staging tools animate; the contract test keeps the list equal to <c>MOVES</c> in <c>addon/moves.py</c>.</summary>
public static class CameraMoves
{
    public static IReadOnlyList<string> All { get; } = ["turntable", "orbit", "dolly", "push_in", "crane"];
}

/// <summary>Areas of the add-on's data directory the volume link reaches, of which only <c>files</c> takes uploads; the contract test keeps the list equal to <c>AREAS</c> in <c>addon/transfer.py</c>.</summary>
public static class VolumeAreas
{
    public const string Files = "files";

    public static IReadOnlyList<string> All { get; } = [Files, "jobs", "batches", "snapshots"];
}

/// <summary>What a render job can be, as the add-on writes it.</summary>
/// <remarks>A job the add-on holds no handle for reads as "running (started by another Blender session)", which is running with a note, so
/// the states are read through <see cref="IsActive"/> rather than compared with a literal.</remarks>
public static class JobStates
{
    public const string Queued = "queued";

    public const string Running = "running";

    public static IReadOnlyList<string> All { get; } = [Queued, Running, "finished", "failed", "cancelled", "interrupted"];

    /// <summary>Still someone's work: queued, running, or running elsewhere.</summary>
    public static bool IsActive(string? state) =>
        state is not null && (state.Equals(Queued, StringComparison.Ordinal) || state.StartsWith(Running, StringComparison.Ordinal));

    /// <summary>Over, whichever way it ended.</summary>
    public static bool HasEnded(string? state) => state is not null && !IsActive(state);
}
