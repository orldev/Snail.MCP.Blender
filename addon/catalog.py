"""What each command means, read once from commands.json.

One file rather than a list in every module: the lease guard, the journal, the Python setting and the server's own guard tests all need
to know whether a command reads or writes, which of its parameters name objects and which name paths. They disagreed before — a command
could be read-only to the journal and destructive to the tool that sent it — and each list was edited on its own.
"""

import json
import os

_FILE = os.path.join(os.path.dirname(os.path.abspath(__file__)), "commands.json")

with open(_FILE, encoding="utf-8") as _handle:
    _SCHEMA = json.load(_handle)

COMMANDS = _SCHEMA["commands"]
TARGET_KEYS = tuple(_SCHEMA["target_keys"])
NAMED_AFTER_SOMETHING_ELSE = tuple(_SCHEMA["named_after_something_else"])
CHANNELS = tuple(_SCHEMA["channels"])
CONTROL = "control"


def of(name):
    return COMMANDS.get(name, {})


def channel_of(name):
    """Which link carries this command: the main thread by default, a socket thread for files and for the heartbeat."""
    return of(name).get("channel") or CONTROL


def answered_off_the_main_thread():
    """The commands whose channel is not the control one; the add-on must register exactly these as immediate."""
    return frozenset(name for name in COMMANDS if channel_of(name) != CONTROL)


def is_read_only(name, params):
    """Only reads: always, or for the actions named — a list is a read even when the command that lists can also write."""
    reading = of(name).get("read_only")
    if reading is True:
        return True
    if not isinstance(reading, dict):
        return False
    actions = reading.get("actions") or []
    chosen = params.get("action") if isinstance(params, dict) else None
    return (chosen or (actions[0] if actions else None)) in actions


def is_unguarded(name):
    """Addresses nothing the lease guard can read: a script, an operator, a batch."""
    return bool(of(name).get("unguarded"))


def paths_of(name):
    """The parameters of this command that name a file or a folder."""
    return tuple(of(name).get("paths") or ())


def targets_of(name):
    """Where in this command's parameters an object or a collection is named, as paths the lease guard walks.

    A path is a parameter's key, with '.' for a key inside a block and '[]' for each item of a list along the way; at the
    end of one a string is taken as it is, a list gives its strings and a block gives its keys. They are written down
    because a name is not always where its key's spelling suggests: an object can be named by focus.object, by
    pivot_name or by being the key of object_motion_blur, and a guard reading a list of likely key names saw none of those.
    """
    return tuple(of(name).get("targets") or ())
