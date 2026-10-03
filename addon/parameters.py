"""The parameters a command takes, and what becomes of the one it does not.

A key nobody reads used to travel the whole way into a handler and be dropped there without a word: the reply said ok,
the work was not done, and the caller looked for the fault in the scene. set_material({"objects": [...]}) assigned
nothing, render_animation({"frame_start": 1, "frame_end": 4}) rendered all two hundred and fifty frames, and
set_camera_optics({"name": "Post"}) quietly moved to whichever camera the scene had. The typed tools never send such a
key, so the refusal cannot live in the server: a step of blender_run, a call inside a program and a hand-written params
all reach the add-on the same way. It lives at the gate every one of them passes.

The names come from commands.json, which the guard test measures against the handlers themselves, so a parameter a
handler starts reading is a parameter callers may send on the same commit.
"""

import difflib
import re

from . import catalog, core
from .server import CommandError

CARRIED_BY_THE_LINK = ("agent",)
SUGGESTIONS = 3
CALL_SETTINGS = {
    "timeout_seconds": "how long to wait is set on the call, not on a command: timeoutSeconds of blender_run, blender_program or the tool",
}


def _register():
    if refuse not in core.GATES:
        core.GATES.append(refuse)


def accepted(name):
    """Every parameter this command reads, plus the ones the link adds on the caller's behalf."""
    return tuple(catalog.of(name).get("params") or ()) + CARRIED_BY_THE_LINK


def refuse(name, params, agent=None):
    """The gate: a parameter the command does not read is a mistake in the request, not noise to drop.

    A command the schema says nothing about is left alone — the schema having no entry is a different fault, and the
    guard test is where it is caught; refusing everything here would only bury it.
    """
    if not isinstance(params, dict) or "params" not in catalog.of(name):
        return
    unknown = [key for key in params if key not in accepted(name)]
    if not unknown:
        return
    declared = tuple(catalog.of(name).get("params") or ())
    hints = {key: _nearest(key, declared) for key in unknown}
    notes = {key: CALL_SETTINGS[_snake(key)] for key in unknown if _snake(key) in CALL_SETTINGS}
    named = ", ".join(_described(key, hints[key], notes.get(key)) for key in unknown)
    raise CommandError("BadRequest", f"'{name}' takes no {named}",
                       {"unknown": unknown, "accepted": sorted(declared), "suggestions": {key: value for key, value in hints.items() if value}, **({"notes": notes} if notes else {})})


def _described(key, hints, note):
    if note:
        return f"'{key}' ({note})"
    return f"'{key}'" + (f" (did you mean {' or '.join(repr(hint) for hint in hints)}?)" if hints else "")


def _snake(key):
    """resolutionX → resolution_x: the spelling of the typed tools, which a step of a sequence does not take."""
    return re.sub(r"(?<=[a-z0-9])(?=[A-Z])", "_", key).lower()


def _nearest(key, declared):
    """What the caller probably meant: the snake_case of a camelCase name, a name spelled almost the same, or the one their name was
    grown from — 'frame_end' is nowhere near 'end' by letters, and is exactly it by intent. Only the command's own parameters are
    offered: 'agent' is added by the link and never belongs in a request."""
    exact = [_snake(key)] if _snake(key) != key and _snake(key) in declared else []
    close = [name for name in difflib.get_close_matches(key, declared, n=SUGGESTIONS, cutoff=0.6) if name not in exact]
    grown = [name for name in declared if name not in exact + close and (key.endswith("_" + name) or key.startswith(name + "_"))]
    return (exact + close + grown)[:SUGGESTIONS]


_register()
