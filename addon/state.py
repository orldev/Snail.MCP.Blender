"""What survives a restart and is shared between Blender processes: small JSON files under the data directory.

The directory comes from SNAIL_MCP_BLENDER_DATA_DIRECTORY, the same variable the server reads, so a worker
Blender the add-on starts inherits it and writes into the same journal. Writes replace the file atomically;
a lock file guards read-modify-write sequences, because two processes may claim the same object at once.
"""

import contextlib
import ctypes
import json
import os
import secrets
import subprocess
import time
import uuid

from .server import CommandError

VARIABLE = "SNAIL_MCP_BLENDER_DATA_DIRECTORY"
LOCK_TIMEOUT_SECONDS = 2.0
STALE_LOCK_SECONDS = 10.0


def directory():
    root = os.environ.get(VARIABLE) or os.path.join(os.path.expanduser("~"), ".snail-mcp-blender")
    path = os.path.join(root, "state")
    os.makedirs(path, exist_ok=True)
    return path


def inherited():
    """The environment a worker Blender starts with: the parent's, plus the data directory so both write the same journal."""
    environment = dict(os.environ)
    environment[VARIABLE] = os.path.dirname(directory())
    return environment


TOKEN_FILE = "token"


def token(preferred=""):
    """The shared secret the bridge demands: the one from the preferences, else one generated once and kept in a file only the user can read."""
    if preferred:
        return preferred
    stored = _read_token()
    if stored:
        return stored
    generated = secrets.token_urlsafe(32)
    target = path(TOKEN_FILE)
    with open(f"{target}.tmp", "w", encoding="utf-8") as handle:
        handle.write(generated + "\n")
    os.chmod(f"{target}.tmp", 0o600)
    os.replace(f"{target}.tmp", target)
    return generated


def _read_token():
    try:
        with open(path(TOKEN_FILE), encoding="utf-8") as handle:
            return handle.read().strip()
    except OSError:
        return ""


def data_directory():
    return os.path.dirname(directory())


PROCESS_QUERY_LIMITED_INFORMATION = 0x1000
STILL_ACTIVE = 259


def alive(pid):
    """Whether a process this add-on started is still running; a pid that cannot be asked about is gone.

    Windows is asked differently on purpose: there os.kill with any signal but the two console events is TerminateProcess,
    so probing a worker with signal 0 would kill the very job whose status is being read.
    """
    if not pid:
        return False
    if os.name == "nt":
        return _alive_on_windows(int(pid))
    try:
        os.kill(int(pid), 0)
        return True
    except OSError:
        return False


def runs(pid, marker):
    """Whether pid is still the process that was started with marker on its command line, such as a worker and its spec file.

    A pid outlives its process: after a restart, and always in a container's fresh PID namespace, the number an old job
    recorded may name any other program, and signalling that one would kill the wrong thing. Where the command line
    cannot be read, which is Windows, a live pid is taken at its word.
    """
    if not alive(pid):
        return False
    command = _command_line(int(pid))
    return command is None or marker in command


def _command_line(pid):
    if os.name == "nt":
        return None
    with contextlib.suppress(OSError):
        with open(f"/proc/{pid}/cmdline", "rb") as handle:
            return handle.read().replace(b"\0", b" ").decode("utf-8", errors="replace")
    try:
        listed = subprocess.run(["ps", "-ww", "-p", str(pid), "-o", "command="], capture_output=True, text=True, timeout=5, check=False)
    except (OSError, subprocess.TimeoutExpired):
        return None
    return listed.stdout if listed.returncode == 0 else ""


def _alive_on_windows(pid):
    kernel32 = ctypes.windll.kernel32
    handle = kernel32.OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, False, pid)
    if not handle:
        return False
    try:
        code = ctypes.c_ulong()
        return bool(kernel32.GetExitCodeProcess(handle, ctypes.byref(code))) and code.value == STILL_ACTIVE
    finally:
        kernel32.CloseHandle(handle)


def path(name):
    return os.path.join(directory(), name)


def load(name, default):
    try:
        with open(path(name), encoding="utf-8") as handle:
            return json.load(handle)
    except (OSError, ValueError):
        return default


def save(name, data):
    """Writes the file whole, through a name of this process's own.

    The staging name used to be the target plus '.tmp', which is the same name in every process that shares the data directory: a batch
    worker and the Blender that started it truncated and filled one file, and whichever renamed first published whatever the other had
    written so far.
    """
    target = path(name)
    pending = f"{target}.{os.getpid()}.tmp"
    with open(pending, "w", encoding="utf-8") as handle:
        json.dump(data, handle, default=str)
    os.replace(pending, target)


def append(name, entry):
    with open(path(name), "a", encoding="utf-8") as handle:
        handle.write(json.dumps(entry, default=str) + "\n")


def lines(name):
    try:
        with open(path(name), encoding="utf-8") as handle:
            return [json.loads(line) for line in handle if line.strip()]
    except (OSError, ValueError):
        return []


def last(name):
    """The last entry of a line file, read from the tail so a long journal costs nothing to append to."""
    target = path(name)
    try:
        with open(target, "rb") as handle:
            handle.seek(0, os.SEEK_END)
            size = handle.tell()
            step = min(size, 4096)
            handle.seek(size - step)
            tail = handle.read(step).decode("utf-8", errors="replace").rstrip("\n").rsplit("\n", 1)[-1]
            return json.loads(tail) if tail.strip() else None
    except (OSError, ValueError):
        return None


def trim(name, keep):
    entries = lines(name)
    if len(entries) <= keep:
        return
    target = path(name)
    pending = f"{target}.{os.getpid()}.tmp"
    with open(pending, "w", encoding="utf-8") as handle:
        for entry in entries[-keep:]:
            handle.write(json.dumps(entry, default=str) + "\n")
    os.replace(pending, target)


class locked:
    """A lock file taken with O_EXCL: portable, and a lock older than STALE_LOCK_SECONDS is treated as left behind by a crash.

    Waiting used to end by taking the lock anyway, which made it a delay rather than a lock: two holders both believed they had it, and the
    first to leave deleted the second's file and let a third in. A wait that runs out now raises, because a caller told 'someone else is in
    there' can say so, and one told nothing at all writes over another's work. The file holds the token of whoever took it, and a holder
    removes only its own: a lock cleared as stale by someone else is not this holder's to delete.
    """

    def __init__(self, name):
        self._path = path(f"{name}.lock")
        self._token = f"{os.getpid()}:{uuid.uuid4().hex}"

    def __enter__(self):
        deadline = time.monotonic() + LOCK_TIMEOUT_SECONDS
        while True:
            try:
                handle = os.open(self._path, os.O_CREAT | os.O_EXCL | os.O_WRONLY)
                os.write(handle, self._token.encode("utf-8"))
                os.close(handle)
                return self
            except FileExistsError:
                if _is_stale(self._path):
                    _remove(self._path)
                    continue
                if time.monotonic() > deadline:
                    raise CommandError("Busy", f"'{os.path.basename(self._path)}' is held by another process; try again") from None
                time.sleep(0.01)

    def __exit__(self, *_):
        if _holder(self._path) == self._token:
            _remove(self._path)
        return False


def _holder(lock_path):
    try:
        with open(lock_path, encoding="utf-8") as handle:
            return handle.read().strip()
    except OSError:
        return None


def _is_stale(lock_path):
    try:
        return time.time() - os.path.getmtime(lock_path) > STALE_LOCK_SECONDS
    except OSError:
        return False


def _remove(lock_path):
    with contextlib.suppress(OSError):
        os.remove(lock_path)
