"""The data directory as something to keep clean: how much each area holds, one folder of it at a time, and deleting what is done with.

Answered from the socket thread like the file transfers, so a page showing it never waits for a render. A deletion refuses
what is still in use - the file open in Blender, a job or batch still queued or running - because whoever asks from a page
cannot see what the Blender behind it is doing. The open file is remembered from the main thread, the socket thread never
reads bpy. Whether it has unsaved changes is remembered too: a Blender without an interface never raises bpy.data.is_dirty
when a command edits data through the API, so every successful command that is neither a read nor a render counts as a
change, until a file is saved, opened or created.
"""

import contextlib
import json
import os
import shutil

import bpy
from bpy.app.handlers import persistent

from . import batch, core, engines, jobs, state, team, transfer
from .core import immediate_command
from .server import CommandError

LIST_LIMIT = 2000
OPEN = {"file": None, "modified": False}
COMPUTE = {}
CHOOSING_THE_GPU = ("set_cycles",)
CLEANING = ("open_file", "new_file", "open_project")
KEEPING_THE_SCENE = ("render_image", "render_job", "render_job_cancel", "batch_job", "render_check")


@immediate_command("storage")
def storage(bridge, params):
    """How many bytes and files each area holds, how much the disk under them has left, and which file Blender has open."""
    disk = shutil.disk_usage(state.data_directory())
    return {
        "data_directory": state.data_directory(),
        "areas": [_measure(area) for area in transfer.AREAS],
        "disk": {"total_bytes": disk.total, "free_bytes": disk.free},
        "open_file": OPEN["file"],
        "modified": has_unsaved_changes(),
        "compute": dict(COMPUTE) or None,
    }


@immediate_command("storage_list")
def storage_list(bridge, params):
    """One folder of an area: every entry with its size, and at the top of jobs, batches and snapshots what each one is."""
    area = params.get("area") or "files"
    relative = _relative(params.get("path"))
    directory = transfer.inside(area, relative)
    if not os.path.isdir(directory):
        if relative:
            raise CommandError("NotFound", f"no folder '{relative}' in the {area} area")
        return {"area": area, "path": "", "directory": directory, "entries": [], "truncated": False, "open_file": OPEN["file"]}

    names = sorted((name for name in os.listdir(directory) if not name.endswith(transfer.PARTIAL_SUFFIX)), key=str.casefold)
    if area == "snapshots" and not relative:
        entries = [_snapshot(directory, name[:-6]) for name in names if name.endswith(".blend")]
    else:
        entries = [_entry(area, directory, name, top=not relative) for name in names[:LIST_LIMIT]]
    return {"area": area, "path": relative, "directory": directory, "entries": entries, "truncated": len(names) > LIST_LIMIT, "open_file": OPEN["file"]}


@immediate_command("storage_delete")
def storage_delete(bridge, params):
    """Deletes a file or folder of an area for good; a snapshot is deleted by its name, with the note beside it."""
    area = params.get("area") or "files"
    relative = _relative(params.get("path"))
    if not relative:
        raise CommandError("BadRequest", "'path' names what to delete inside the area; an area itself is never deleted")
    target = transfer.inside(area, relative)
    if target == transfer.area_root(area):
        raise CommandError("BadRequest", "'path' names the area itself, which is never deleted")
    if area == "snapshots" and not os.path.lexists(target) and os.path.isfile(f"{target}.blend"):
        return _delete_snapshot(area, relative, target)
    if not os.path.lexists(target):
        raise CommandError("NotFound", f"nothing at '{relative}' in the {area} area")

    _refuse_in_use(area, relative, target)
    size, count = _size(target)
    if os.path.isdir(target) and not os.path.islink(target):
        shutil.rmtree(target)
    else:
        os.remove(target)
    return {"area": area, "path": relative, "deleted_files": count, "bytes": size}


def _relative(path):
    return (path or "").strip().replace("\\", "/").strip("/")


def _measure(area):
    root = transfer.area_root(area)
    size, count = _size(root)
    return {"area": area, "directory": root, "bytes": size, "files": count}


def _size(path):
    if not os.path.isdir(path) or os.path.islink(path):
        with contextlib.suppress(OSError):
            return os.path.getsize(path), 1
        return 0, 0
    total = count = 0
    for directory, _, names in os.walk(path):
        for name in names:
            with contextlib.suppress(OSError):
                total += os.path.getsize(os.path.join(directory, name))
                count += 1
    return total, count


def _entry(area, directory, name, top):
    path = os.path.join(directory, name)
    size, count = _size(path)
    entry = {"name": name, "kind": "directory" if os.path.isdir(path) else "file", "bytes": size, "files": count, "modified": _modified(path)}
    if top and area == "jobs" and name.startswith("job-") and os.path.isdir(path):
        entry["job"] = _job(name, path)
    if top and area == "batches" and name.startswith("batch-") and os.path.isdir(path):
        entry["batch"] = _batch(name, path)
    return entry


def _snapshot(directory, name):
    blend = os.path.join(directory, f"{name}.blend")
    note = os.path.join(directory, f"{name}.json")
    meta = {}
    with contextlib.suppress(OSError, ValueError):
        with open(note, encoding="utf-8") as handle:
            meta = json.load(handle)
    size = sum(os.path.getsize(path) for path in (blend, note) if os.path.isfile(path))
    return {"name": name, "kind": "snapshot", "bytes": size, "files": 2 if os.path.isfile(note) else 1, "modified": _modified(blend),
            "snapshot": {"saved_at": meta.get("saved_at"), "note": meta.get("note") or None, "origin": meta.get("origin")}}


def _job(job_id, directory):
    status = jobs._job_status(job_id, directory, 0)
    return {key: status.get(key) for key in ("state", "frames_done", "frames_total", "elapsed_s", "error")}


def _batch(batch_id, directory):
    status = batch._batch_status(batch_id, directory, 0)
    return {key: status.get(key) for key in ("state", "steps_done", "steps_total", "elapsed_s", "error")}


def _modified(path):
    try:
        return round(os.path.getmtime(path))
    except OSError:
        return None


def _refuse_in_use(area, relative, target):
    if area == "files" and OPEN["file"] and _contains(target, OPEN["file"]):
        raise CommandError("InUse", f"'{relative}' holds {OPEN['file']}, the file open in Blender; open another file before deleting it", {"open_file": OPEN["file"]})
    root = transfer.area_root(area)
    top = os.path.relpath(target, root).split(os.sep, 1)[0]
    if area == "jobs" and top.startswith("job-") and _is_active(_job(top, os.path.join(root, top))["state"]):
        raise CommandError("InUse", f"job '{top}' is still running; cancel it before deleting it", {"job": top})
    if area == "batches" and top.startswith("batch-") and _is_active(_batch(top, os.path.join(root, top))["state"]):
        raise CommandError("InUse", f"batch '{top}' is still running; wait for it before deleting it", {"batch": top})


def _is_active(phase):
    return jobs.is_active(phase)


def _delete_snapshot(area, name, target):
    deleted = [path for path in (f"{target}.blend", f"{target}.json") if os.path.isfile(path)]
    size = sum(os.path.getsize(path) for path in deleted)
    for path in deleted:
        os.remove(path)
    return {"area": area, "path": name, "deleted_files": len(deleted), "bytes": size}


def _contains(parent, child):
    try:
        parent, child = os.path.normcase(os.path.abspath(parent)), os.path.normcase(os.path.abspath(child))
        return os.path.commonpath([parent, child]) == parent
    except ValueError:
        return False


def has_unsaved_changes():
    """Whether the open file has changes a new or opened file would drop; read on the main thread as well as the socket thread."""
    return OPEN["modified"]


@persistent
def _on_file(*_):
    OPEN.update({"file": bpy.data.filepath or None, "modified": False})


def remember_compute():
    """The backend and devices Cycles renders with, read on the main thread where preferences may be touched, for the socket thread to report."""
    COMPUTE.clear()
    COMPUTE.update(engines._compute() or {})


def _after(name, params, agent, ok, error):
    OPEN["file"] = bpy.data.filepath or None
    if not ok:
        return
    if name in CHOOSING_THE_GPU:
        remember_compute()
    if name in CLEANING or (name == "save_file" and not params.get("copy")):
        OPEN["modified"] = False
    elif name not in KEEPING_THE_SCENE and not team.is_read_only(name, params):
        OPEN["modified"] = True


def _watch_files():
    for handlers in (bpy.app.handlers.load_post, bpy.app.handlers.save_post):
        if _on_file not in handlers:
            handlers.append(_on_file)
    if _after not in core.AFTER:
        core.AFTER.append(_after)


_watch_files()
