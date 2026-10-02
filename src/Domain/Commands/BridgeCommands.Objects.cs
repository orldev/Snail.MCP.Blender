namespace Snail.MCP.Blender.Domain.Commands;

/// <summary>Commands of <c>addon/objects.py</c>: primitives, text and empties, one object in depth, selection, deletion, duplication, transforms and parenting.</summary>
public static partial class BridgeCommands
{
    public static readonly BridgeCommand ObjectInfo = new("object_info", AddOnModules.Objects);

    public static readonly BridgeCommand AddPrimitive = new("add_primitive", AddOnModules.Objects);

    public static readonly BridgeCommand SelectObjects = new("select_objects", AddOnModules.Objects);

    public static readonly BridgeCommand DeleteObjects = new("delete_objects", AddOnModules.Objects);

    public static readonly BridgeCommand DuplicateObject = new("duplicate_object", AddOnModules.Objects);

    public static readonly BridgeCommand TransformObject = new("transform_object", AddOnModules.Objects);

    public static readonly BridgeCommand UpdateObject = new("update_object", AddOnModules.Objects);

    public static readonly BridgeCommand AddText = new("add_text", AddOnModules.Objects);

    public static readonly BridgeCommand AddEmpty = new("add_empty", AddOnModules.Objects);
}
