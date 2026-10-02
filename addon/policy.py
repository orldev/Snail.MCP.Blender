"""What this Blender lets through, whoever asks.

The server has a setting for how far Python may go, but the server is not the boundary: anything holding the token can speak to this
add-on directly, and a second server may be configured differently. So the rule lives here too, where the power is. Off is the hard
one: no scripts, no operator that installs or trusts code, and no file written outside the add-on's own areas. Fallback is the
server's business — it reads a script to see whether a tool already does that work, which needs the tool catalog — so here it reads
as allow.
"""

import os

from . import catalog, core, state, transfer
from .server import CommandError

ACCESS = {"python": "allow"}
LEVELS = ("allow", "fallback", "off")
SCRIPT_MODULES = ("script.", "console.", "preferences.", "extensions.")


def _register():
    if refuse not in core.GATES:
        core.GATES.append(refuse)


def choose(access):
    """Sets how far Python may go here; anything but the three levels is refused at start-up rather than read as allow."""
    level = (access or "allow").strip().lower()
    if level not in LEVELS:
        raise CommandError("BadRequest", f"python access must be one of {', '.join(LEVELS)}")
    ACCESS["python"] = level
    return level


def is_open():
    return ACCESS["python"] != "off"


def refuse(name, params, agent):
    """The BEFORE hook: with Python off, the commands that run code or write outside the areas never reach their handler."""
    if is_open() or not isinstance(params, dict):
        return
    if name == "python":
        raise CommandError("PythonDisabled", "this Blender runs with python off; use the commands instead")
    if name == "run_operator" and _runs_scripts(params):
        raise CommandError("PythonDisabled", f"'{params.get('name')}' runs code, and this Blender runs with python off")
    for key in catalog.paths_of(name):
        if not _inside_the_data_directory(params.get("area"), params.get(key)):
            raise CommandError("PythonDisabled", f"'{key}' leaves the add-on's own folders, and this Blender runs with python off")


def _runs_scripts(params):
    name = str(params.get("name") or "").strip().lower()
    properties = params.get("params") or {}
    return (name.startswith(SCRIPT_MODULES)
            or name == "text.run_script"
            or (isinstance(properties, dict) and bool(properties.get("use_scripts"))))


def _inside_the_data_directory(area, value):
    """A path the add-on owns: named by its area, written relative to the files area, or absolute inside the data directory — which is what
    a render output under files/<project>/renders looks like."""
    if value is None or value == "":
        return True
    if area:
        return True
    if os.path.isabs(os.path.expanduser(str(value))):
        root = state.data_directory()
        resolved = os.path.normpath(os.path.expanduser(str(value)))
        return os.path.commonpath([root, resolved]) == root
    try:
        transfer.inside("files", str(value))
    except CommandError:
        return False
    return True


_register()
