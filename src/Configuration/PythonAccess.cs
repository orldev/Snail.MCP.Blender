namespace Snail.MCP.Blender.Configuration;

/// <summary>How much of Blender's Python this server lets through, from the most open to the most closed.</summary>
/// <remarks>One setting rather than a flag and an exception: a script is either the last resort, the last resort with a
/// rule the server can check, or not available at all.</remarks>
public enum PythonAccess
{
    /// <summary>blender_python runs what it is given; the reply names the tools that already cover what the script did.</summary>
    Allow,

    /// <summary>blender_python runs only what the catalog cannot: a script whose operations are covered by tools is refused and names them.</summary>
    /// <remarks>It steers, it does not guard: the script is read as text, so one written to avoid the reading, with the datablock in a local
    /// variable for instance, still runs. <see cref="Off"/> is the setting that keeps Python out.</remarks>
    Fallback,

    /// <summary>blender_python, the operators that run scripts or install code and batches containing them are all refused.</summary>
    Off,
}
