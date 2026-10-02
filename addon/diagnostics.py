"""Answered from the socket thread: whether Blender is alive, what the render in flight is doing, and which build of the add-on Blender has installed."""

import hashlib
import os
import re
import sys
import time

import bpy

from . import state
from .core import COMMANDS, IMMEDIATE, immediate_command
from .transfer import files_root

IDENTITY = {}

PROTOCOL = 1
"""What this add-on speaks. Raised only when a change breaks a server that was written for the number before it."""

CAPABILITIES = ("channels", "deadlines", "policy", "job-states", "pulse")
"""What this add-on can do, by name, for a server that has to work with an older one.

A digest says two sides differ; it cannot say what the difference costs. These say it: 'channels' answers the file and heartbeat commands
off the main thread, as commands.json marks them; 'deadlines' drops a queued request whose caller stopped waiting; 'policy' enforces the
Python setting and the allowed paths here, where the power is; 'job-states' keeps render jobs as one closed set of states and logs every
frame it writes; 'pulse' says how long ago the main thread last drained the queue, so a Blender whose dispatcher died can be told from a
Blender with nothing to do.
"""


def _identity():
    """The version and source digest of this installed copy, so the server can tell a stale install from the one it ships.

    The version alone cannot answer that: it stays put across bug fixes. The digest covers the add-on's own Python and the command
    schema beside it, hashed the way the server hashes what it ships — the top-level .py and .json files in name order, each as its
    name in UTF-8 then its bytes. The schema belongs in it because it says what each command means, and a server reading one set of
    meanings while Blender acts on another is exactly what this check is for. The files cannot change while Blender runs, so it is
    read once.
    """
    if not IDENTITY:
        directory = os.path.dirname(os.path.abspath(__file__))
        digest = hashlib.sha256()
        for name in sorted(entry for entry in os.listdir(directory) if entry.endswith((".py", ".json"))):
            digest.update(name.encode())
            with open(os.path.join(directory, name), "rb") as handle:
                digest.update(handle.read())
        IDENTITY.update({"version": _manifest_version(directory), "digest": digest.hexdigest()[:12]})
    return dict(IDENTITY)


def _manifest_version(directory):
    try:
        with open(os.path.join(directory, "blender_manifest.toml"), encoding="utf-8") as handle:
            found = re.search(r'^version\s*=\s*"([^"]+)"', handle.read(), re.MULTILINE)
    except OSError:
        return None
    return found.group(1) if found else None


BLENDER = {}


def _blender():
    """What ping says about this Blender, read once and answered from a plain dict afterwards.

    ping is answered on the socket thread, and the rule of this add-on is that the socket thread never touches bpy. These three are fixed
    for the life of the process — the version, the path of the binary and whether there is an interface — so reading them the first time
    ping is asked, and on the main thread when the add-on starts, costs nothing and keeps the rule whole.
    """
    if not BLENDER:
        BLENDER.update({"version": bpy.app.version_string, "binary": bpy.app.binary_path, "background": bpy.app.background})
    return BLENDER


_blender()


@immediate_command("ping")
def ping(bridge, params):
    addon = _identity()
    addon["protocol"] = PROTOCOL
    addon["capabilities"] = list(CAPABILITIES)
    addon["commands"] = len(COMMANDS) + len(IMMEDIATE)
    if params.get("commands"):
        addon["command_names"] = sorted([*COMMANDS, *IMMEDIATE])
    return {
        "blender": _blender()["version"],
        "busy": bridge.executing is not None,
        "executing": bridge.executing,
        "queued": bridge.queued,
        "pumped_s_ago": bridge.since_pumped,
        "addon": addon,
        "machine": _machine(),
    }


def _machine():
    """Where Blender runs. Every path a tool is given is a path on this machine, which is not the server's when Blender is remote."""
    return {
        "platform": sys.platform,
        "home": os.path.expanduser("~"),
        "data_directory": state.data_directory(),
        "files": files_root(),
        "binary": _blender()["binary"],
        "background": _blender()["background"],
    }


PROGRESS = {"active": False, "phase": None, "frame": None, "frames_done": 0, "step": 0, "steps": 0, "started_at": None, "elapsed_s": 0.0, "stats": None, "peak_memory_mb": 0.0, "files": []}


@immediate_command("progress")
def progress(bridge, params):
    """What the render in flight is doing; read from a plain dict the render handlers fill on the main thread, so the socket thread touches no bpy."""
    snapshot = dict(PROGRESS)
    if snapshot["started_at"]:
        snapshot["elapsed_s"] = round(time.time() - snapshot["started_at"], 1)
    snapshot["executing"] = bridge.executing
    snapshot["files"] = snapshot["files"][-5:]
    return snapshot
