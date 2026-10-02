namespace Snail.MCP.Blender.Domain;

/// <summary>The Python modules of the add-on, each the home of a group of commands; every command names its module, so the contract is checked file by file.</summary>
public static class AddOnModules
{
    public const string Animation = "animation";

    public const string Assets = "assets";

    public const string Batch = "batch";

    public const string Budget = "budget";

    public const string Cameras = "cameras";

    public const string Collections = "collections";

    public const string Compositor = "compositor";

    public const string Constraints = "constraints";

    public const string Diagnostics = "diagnostics";

    public const string Effects = "effects";

    public const string Engines = "engines";

    public const string Files = "files";

    public const string Images = "images";

    public const string Interchange = "interchange";

    public const string Jobs = "jobs";

    public const string Layers = "layers";

    public const string Lights = "lights";

    public const string Materials = "materials";

    public const string Modeling = "modeling";

    public const string Moves = "moves";

    public const string Objects = "objects";

    public const string Operators = "operators";

    public const string Optics = "optics";

    public const string Output = "output";

    public const string Physics = "physics";

    public const string Rendering = "rendering";

    public const string Scene = "scene";

    public const string Scenes = "scenes";

    public const string Sequence = "sequence";

    public const string Sequencer = "sequencer";

    public const string Storage = "storage";

    public const string Team = "team";

    public const string Transfer = "transfer";

    public const string Visibility = "visibility";

    public const string World = "world";

    /// <summary>Every module that registers commands.</summary>
    public static IReadOnlyList<string> All { get; } = [Animation, Assets, Batch, Budget, Cameras, Collections, Compositor, Constraints, Diagnostics, Effects, Engines, Files, Images, Interchange, Jobs, Layers, Lights, Materials, Modeling, Moves, Objects, Operators, Optics, Output, Physics, Rendering, Scene, Scenes, Sequence, Sequencer, Storage, Team, Transfer, Visibility, World];
}
