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

from . import catalog, core
from .server import CommandError

CARRIED_BY_THE_LINK = ("agent",)
SUGGESTIONS = 3


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
    known = accepted(name)
    unknown = [key for key in params if key not in known]
    if not unknown:
        return
    hints = {key: _nearest(key, known) for key in unknown}
    named = ", ".join(f"'{key}'" + (f" (did you mean {' or '.join(repr(hint) for hint in hints[key])}?)" if hints[key] else "") for key in unknown)
    raise CommandError("BadRequest", f"'{name}' takes no {named}", {"unknown": unknown, "accepted": sorted(known), "suggestions": {key: value for key, value in hints.items() if value}})


def _nearest(key, known):
    """What the caller probably meant: a name spelled almost the same, or the one their name was grown from — 'frame_end'
    is nowhere near 'end' by letters, and is exactly it by intent."""
    close = difflib.get_close_matches(key, known, n=SUGGESTIONS, cutoff=0.6)
    grown = [name for name in known if name not in close and (key.endswith("_" + name) or key.startswith(name + "_"))]
    return (close + grown)[:SUGGESTIONS]


_register()
