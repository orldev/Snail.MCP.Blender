"""Undo history, the .blend file itself and the project folder that holds it."""

import os

import bpy

from . import storage, transfer
from .core import _require, command
from .objects import _window_override
from .server import CommandError

PROJECT_FOLDERS = ("renders", "textures")
PROJECT_STARTS = ("empty", "current")
MAX_PROJECT_NAME = 100
RESERVED_CHARACTERS = '/\\:*?"<>|'


@command("undo_redo")
def undo_redo(params):
    action = params.get("action") or "undo"
    if action not in ("undo", "redo"):
        raise CommandError("BadRequest", "'action' must be undo or redo")
    steps = max(1, int(params.get("steps") or 1))
    operator = bpy.ops.ed.undo if action == "undo" else bpy.ops.ed.redo
    done = 0
    with _window_override():
        for _ in range(steps):
            if not operator.poll():
                break
            operator()
            done += 1
    return {"action": action, "steps": done}


@command("save_file")
def save_file(params):
    path = params.get("path") or bpy.data.filepath
    if not path:
        raise CommandError("BadRequest", "the file has never been saved; pass 'path'")
    directory = os.path.dirname(path)
    if directory:  # Blender reports a missing folder as "Cannot open file …@ for writing", which names neither the folder nor the cure
        os.makedirs(directory, exist_ok=True)
    with _window_override():
        _save(path, bool(params.get("copy")))
    return {"path": path, "saved": True}


@command("open_file")
def open_file(params):
    path = _require(params, "path")
    with _window_override():
        bpy.ops.wm.open_mainfile(filepath=path, load_ui=bool(params.get("load_ui")))
    return {"path": bpy.data.filepath, "scene": bpy.context.scene.name}


@command("new_file")
def new_file(params):
    with _window_override():
        bpy.ops.wm.read_homefile(use_empty=bool(params.get("empty")))
    return {"scene": bpy.context.scene.name, "objects": len(bpy.context.scene.objects)}


@command("open_project")
def open_project(params):
    """Opens the project named after a folder under files/, creating <name>.blend, renders/ and textures/ when it does not exist.

    A project is one folder so that a whole piece of work uploads, downloads and is deleted as one; the new file renders
    into //renders/ and its external paths are kept relative for the same reason.
    """
    name = _project_name(params)
    start = params.get("from") or "empty"
    if start not in PROJECT_STARTS:
        raise CommandError("BadRequest", "'from' must be empty or current")
    directory = os.path.join(transfer.files_root(), name)
    blend = os.path.join(directory, f"{name}.blend")
    exists = os.path.isfile(blend)
    if exists and start == "current":
        raise CommandError("Exists", f"project '{name}' already has {name}.blend; open it without 'from' or pick another name", {"blend": blend})
    if (bpy.data.is_dirty or storage.has_unsaved_changes()) and start == "empty" and not params.get("discard"):
        raise CommandError("Unsaved", "the open file has unsaved changes; save it first or pass discard to drop them", {"file": bpy.data.filepath or None})

    for folder in PROJECT_FOLDERS:
        os.makedirs(os.path.join(directory, folder), exist_ok=True)
    with _window_override():
        if exists:
            bpy.ops.wm.open_mainfile(filepath=blend, load_ui=False)
        else:
            if start == "empty":
                bpy.ops.wm.read_homefile(use_empty=True)
            bpy.context.scene.render.filepath = "//renders/"
            _save(blend, False)

    scene = bpy.context.scene
    return {"name": name, "created": not exists, "blend": blend, "directory": directory,
            **{folder: os.path.join(directory, folder) for folder in PROJECT_FOLDERS},
            "scene": scene.name, "objects": len(scene.objects)}


def _project_name(params):
    name = _require(params, "name").strip()
    if name in (".", "..") or len(name) > MAX_PROJECT_NAME or name != name.rstrip(". ") or any(character in RESERVED_CHARACTERS or ord(character) < 32 for character in name):
        raise CommandError("BadRequest", f"'name' must be one folder name: no slashes or {RESERVED_CHARACTERS}, no trailing dot or space, at most {MAX_PROJECT_NAME} characters", {"name": name})
    return name


def _save(path, copy):
    """Saves, and inside files/ keeps external paths relative, so a project downloaded to another machine still finds its textures."""
    if not _inside_files(path):
        bpy.ops.wm.save_as_mainfile(filepath=path, copy=copy)
        return
    if bpy.data.filepath:
        bpy.ops.file.make_paths_relative()
        bpy.ops.wm.save_as_mainfile(filepath=path, copy=copy)
        return
    bpy.ops.wm.save_as_mainfile(filepath=path, copy=copy)
    if not copy:
        bpy.ops.file.make_paths_relative()
        bpy.ops.wm.save_mainfile()


def _inside_files(path):
    root = os.path.normcase(os.path.abspath(transfer.files_root()))
    target = os.path.normcase(os.path.abspath(bpy.path.abspath(path)))
    try:
        return os.path.commonpath([root, target]) == root
    except ValueError:
        return False
