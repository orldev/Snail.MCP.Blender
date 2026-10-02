"""The command registry and its dispatcher: what the MCP server may ask Blender to do, and the hooks other modules wrap around every call.

Every handler runs on the main thread with a params dict and returns JSON-safe data; handlers decorated
as immediate run on the socket thread and must not touch bpy state. Modules register themselves here
and this module imports none of them, so there is no cycle to break with a lazy import.

A gate is the one check both kinds pass: what this Blender lets through at all. The BEFORE hooks run on the main thread only, which
is how a transfer, answered from the socket thread, once went past a rule everything else obeyed.
"""

import functools

import bpy

from .server import CommandError

COMMANDS = {}
IMMEDIATE = {}
GATES = []
BEFORE = []
AFTER = []
AGENT_AWARE = set()


def command(name):
    def register(handler):
        COMMANDS[name] = handler
        return handler
    return register


def immediate_command(name):
    def register(handler):
        @functools.wraps(handler)
        def guarded(bridge, params):
            keep(name, params)
            return handler(bridge, params)
        IMMEDIATE[name] = guarded
        return handler
    return register


def keep(name, params, agent=None):
    """Every gate this Blender keeps, for a command of either kind."""
    for gate in GATES:
        gate(name, params, agent)


def dispatch(name, params):
    """Runs one command through the hooks: every BEFORE hook may refuse it, every AFTER hook sees how it ended."""
    handler = COMMANDS.get(name)
    if handler is None:
        raise CommandError("UnknownCommand", f"'{name}' is not a bridge command", {"known": sorted(COMMANDS)})
    agent = params.pop("agent", None) if isinstance(params, dict) else None
    keep(name, params, agent)
    for before in BEFORE:
        before(name, params, agent)
    if name in AGENT_AWARE:
        params["agent"] = agent
    try:
        result = handler(params)
    except Exception as error:
        for after in AFTER:
            after(name, params, agent, False, f"{type(error).__name__}: {error}")
        raise
    for after in AFTER:
        after(name, params, agent, True, None)
    return result


def _require(params, key, kind=str):
    value = params.get(key)
    if not isinstance(value, kind) or (kind is str and not value):
        raise CommandError("BadRequest", f"'{key}' is required and must be a {kind.__name__}")
    return value


def _scene(params):
    """The scene a command acts on: named, or the one in context."""
    name = params.get("scene")
    if not name:
        return bpy.context.scene
    scene = bpy.data.scenes.get(name)
    if scene is None:
        raise CommandError("NotFound", f"no scene named '{name}'", {"scenes": [item.name for item in bpy.data.scenes]})
    return scene


def _object(name):
    item = bpy.data.objects.get(name)
    if item is None:
        raise CommandError("NotFound", f"no object named '{name}'", {"objects": [item.name for item in bpy.data.objects][:50]})
    return item


def selected_objects():
    """The selected objects, read from the view layer rather than from the context.

    bpy.context.selected_objects exists only where a window does. A Blender without an interface has one until a file is opened, and none
    afterwards — so a scene_info that worked at start-up raised AttributeError as soon as a project was opened, which is the first thing a
    client does over HTTP.
    """
    layer = getattr(bpy.context, "view_layer", None)
    if layer is None:
        return []
    return [item for item in layer.objects if item.select_get()]


def _name(datablock):
    return datablock.name if datablock is not None else None


def _set_enum(target, attribute, value, label):
    """Assigns an enum whose items Blender only reveals at assignment time (OCIO, denoisers), turning the failure into a typed error with the known values."""
    try:
        setattr(target, attribute, value)
    except TypeError as error:
        raise CommandError("BadRequest", f"unknown {label} '{value}'", {"known": _enum_from_error(error)}) from error


def _enum_from_error(error):
    text = str(error)
    start, end = text.find("("), text.rfind(")")
    if start < 0 or end < start:
        return []
    return [item.strip().strip("'") for item in text[start + 1:end].split(", ")]


def _known_enum(target, attribute):
    """Every value an enum accepts, including dynamic ones bl_rna lists as NONE."""
    items = [item.identifier for item in target.bl_rna.properties[attribute].enum_items]
    if items and items != ["NONE"]:
        return items
    try:
        setattr(target, attribute, "\u0000snail")
    except TypeError as error:
        return _enum_from_error(error)
    return items
