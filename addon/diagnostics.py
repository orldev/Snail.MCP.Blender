"""Answered from the socket thread: whether Blender is alive, what the render in flight is doing, and which build of the add-on Blender has installed."""

import hashlib
import os
import re
import sys
import tempfile
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

STARTED_AT = time.time()
"""When this process began, so a Blender that has just come back from a crash can be told from one that has been up all day."""

CRASH = {}
"""The crash report the Blender before this one left behind, found once on the main thread at start-up."""


def _blender():
    """What ping says about this Blender, read once and answered from a plain dict afterwards.

    ping is answered on the socket thread, and the rule of this add-on is that the socket thread never touches bpy. These three are fixed
    for the life of the process — the version, the path of the binary and whether there is an interface — so reading them the first time
    ping is asked, and on the main thread when the add-on starts, costs nothing and keeps the rule whole.
    """
    if not BLENDER:
        BLENDER.update({"version": bpy.app.version_string, "binary": bpy.app.binary_path, "background": bpy.app.background})
    return BLENDER


def _look_for_a_crash_report():
    """Finds the trace a Blender that died left behind, because nothing else will.

    A process that goes down takes the scene with it and tells the next one nothing: the server reports a dropped link, the client's next
    call reconnects onto a Blender that quietly lost everything since the last save, and the reason is never named. Blender's own handler
    does leave something — <file>.crash.txt in the folder its temp directory sits in — and the add-on that comes up afterwards is the only
    thing in a position to find it. The newest one is kept here with the moment it was written; the age is answered fresh on every ping,
    because a report from last week belongs to some other day and only its reader can tell which.
    """
    newest = None

    for folder in _temp_folders():
        try:
            names = os.listdir(folder)
        except OSError:
            continue

        for name in names:
            if not name.endswith(".crash.txt"):
                continue

            path = os.path.join(folder, name)

            try:
                written = os.path.getmtime(path)
            except OSError:
                continue

            if newest is None or written > newest[1]:
                newest = (path, written)

    if newest is not None:
        CRASH.update({"path": newest[0], "written_at": time.strftime("%Y-%m-%d %H:%M:%S", time.localtime(newest[1])), "written": newest[1]})


def _temp_folders():
    """Where a crash report lands: the folder this Blender's own temp directory sits in, and the system one when they are not the same."""
    base = os.path.dirname(os.path.normpath(bpy.app.tempdir)) if bpy.app.tempdir else ""

    return list(dict.fromkeys(folder for folder in (base, tempfile.gettempdir()) if folder))


def _crash():
    """The crash report as ping answers it, its age taken now rather than at start-up."""
    if not CRASH:
        return None

    return {"path": CRASH["path"], "written_at": CRASH["written_at"], "age_s": round(time.time() - CRASH["written"], 1)}


_blender()
_look_for_a_crash_report()


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
    """Where Blender runs, and which run of it this is.

    Every path a tool is given is a path on this machine, which is not the server's when Blender is remote. The process also says who it
    is: a Blender that crashed and came back answers exactly like the one that died, so the pid and the uptime are what tell a fresh
    process from the one a client was working with, and the crash report names what happened to its predecessor.
    """
    return {
        "platform": sys.platform,
        "home": os.path.expanduser("~"),
        "data_directory": state.data_directory(),
        "files": files_root(),
        "binary": _blender()["binary"],
        "background": _blender()["background"],
        "pid": os.getpid(),
        "uptime_s": round(time.time() - STARTED_AT, 1),
        "crash_report": _crash(),
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
