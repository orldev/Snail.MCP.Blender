"""Scenes of the file and snapshots of the file itself: several shots in one .blend, and a saved copy to fall back to."""

import json
import os
import time

import bpy

from . import state
from .core import _require, _scene, command
from .server import CommandError

COPY_MODES = ("EMPTY", "LINK_COPY", "FULL_COPY")


def _scene_summary(scene):
    return {
        "name": scene.name,
        "active": scene == bpy.context.scene,
        "camera": scene.camera.name if scene.camera else None,
        "objects": len(scene.objects),
        "frames": [scene.frame_start, scene.frame_end],
        "engine": scene.render.engine,
        "resolution": [scene.render.resolution_x, scene.render.resolution_y],
        "view_layers": [layer.name for layer in scene.view_layers],
    }


def _activate(scene):
    window = bpy.context.window
    if window is None:
        raise CommandError("NoArea", "activating a scene needs Blender's window; in background pass scene to the tools instead")
    window.scene = scene


@command("scenes")
def scenes(params):
    action = (params.get("action") or "list").lower()
    if action == "list":
        return {"active": bpy.context.scene.name, "scenes": [_scene_summary(scene) for scene in bpy.data.scenes]}
    if action == "create":
        name = _require(params, "name")
        mode = (params.get("copy") or "EMPTY").upper()
        if mode not in COPY_MODES:
            raise CommandError("BadRequest", f"copy must be one of {', '.join(COPY_MODES)}")
        source = _scene(params)
        if mode == "EMPTY":
            scene = bpy.data.scenes.new(name)
        elif mode == "LINK_COPY":
            scene = source.copy()
            scene.name = name
            scene.use_fake_user = True
        else:
            window = bpy.context.window
            if window is None:
                raise CommandError("NoArea", "a full copy needs Blender's window; use LINK_COPY in background")
            with bpy.context.temp_override(window=window, scene=source):
                bpy.ops.scene.new(type="FULL_COPY")
            scene = bpy.context.window.scene
            scene.name = name
        if params.get("activate"):
            _activate(scene)
        return _scene_summary(scene)
    scene = bpy.data.scenes.get(_require(params, "name"))
    if scene is None:
        raise CommandError("NotFound", f"no scene named '{params['name']}'", {"known": [item.name for item in bpy.data.scenes]})
    if action == "activate":
        _activate(scene)
        return _scene_summary(scene)
    if action == "rename":
        scene.name = _require(params, "new_name")
        return _scene_summary(scene)
    if action == "remove":
        if len(bpy.data.scenes) == 1:
            raise CommandError("BadRequest", "the file keeps at least one scene")
        name = scene.name
        bpy.data.scenes.remove(scene)
        return {"removed": name, "scenes": [item.name for item in bpy.data.scenes]}
    raise CommandError("BadRequest", "action must be list, create, activate, rename or remove")


def _snapshot_directory(params):
    directory = params.get("directory") or os.path.join(state.data_directory(), "snapshots")
    os.makedirs(directory, exist_ok=True)
    return directory


def _snapshot_entry(directory, name):
    blend = os.path.join(directory, f"{name}.blend")
    meta_path = os.path.join(directory, f"{name}.json")
    meta = {}
    if os.path.isfile(meta_path):
        with open(meta_path) as handle:
            meta = json.load(handle)
    return {"name": name, "path": blend, "bytes": os.path.getsize(blend) if os.path.isfile(blend) else 0, **meta}


@command("snapshot")
def snapshot(params):
    action = (params.get("action") or "save").lower()
    directory = _snapshot_directory(params)
    if action == "list":
        names = sorted(entry[:-6] for entry in os.listdir(directory) if entry.endswith(".blend"))
        return {"directory": directory, "snapshots": [_snapshot_entry(directory, name) for name in names]}
    name = params.get("name") or time.strftime("snapshot-%Y%m%d-%H%M%S")
    if any(character in name for character in "/\\"):
        raise CommandError("BadRequest", "a snapshot name is a file name without folders")
    blend = os.path.join(directory, f"{name}.blend")
    if action == "save":
        bpy.ops.wm.save_as_mainfile(filepath=blend, copy=True, relative_remap=True)
        meta = {"origin": bpy.data.filepath or None, "saved_at": time.strftime("%Y-%m-%d %H:%M:%S"), "note": params.get("note") or ""}
        with open(os.path.join(directory, f"{name}.json"), "w") as handle:
            json.dump(meta, handle)
        return _snapshot_entry(directory, name)
    if not os.path.isfile(blend):
        raise CommandError("NotFound", f"no snapshot named '{name}'", {"known": sorted(entry[:-6] for entry in os.listdir(directory) if entry.endswith(".blend"))})
    if action == "restore":
        entry = _snapshot_entry(directory, name)
        bpy.ops.wm.open_mainfile(filepath=blend, load_ui=False)
        origin = entry.get("origin")
        if origin:
            bpy.ops.wm.save_as_mainfile(filepath=origin, relative_remap=True)
        note = None if origin else "the origin was never saved, so the file now lives at the snapshot path; blender_save_file with a path moves it"
        return {**entry, "restored": True, "file": bpy.data.filepath or None, "note": note}
    if action == "remove":
        os.remove(blend)
        meta_path = os.path.join(directory, f"{name}.json")
        if os.path.isfile(meta_path):
            os.remove(meta_path)
        return {"removed": name}
    raise CommandError("BadRequest", "action must be save, restore, list or remove")
