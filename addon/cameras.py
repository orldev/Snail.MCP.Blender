"""Camera objects and their lens settings."""

import bpy
import mathutils

from .core import _name, _object, _require, _scene, command
from .objects import _degrees, _link_new_object, _object_summary, _radians, _vector
from .server import CommandError


def _camera_of(params):
    """The camera a command works on: named by 'camera', or by 'name' as add_camera and set_camera name the same object; the scene's otherwise.

    Both spellings are taken because only one of them used to be, and the other was dropped without a word: set_camera_optics sent with
    'name' worked on whichever camera the scene had, so a lens meant for one camera landed on another and the two looked as though they
    shared their data.
    """
    named = params.get("camera") or params.get("name")
    if named:
        item = _object(named)
    else:
        item = _scene(params).camera
    if item is None or item.type != "CAMERA":
        raise CommandError("BadRequest", "no camera: pass camera or name, or set one with blender_render_settings")
    return item


@command("add_camera")
def add_camera(params):
    name = params.get("name") or "Camera"
    data = bpy.data.cameras.new(name)
    item = _link_new_object(bpy.data.objects.new(name, data))
    item.location = _vector(params, "location", (7.36, -6.93, 4.96))
    item.rotation_euler = _radians(_vector(params, "rotation", (63.6, 0.0, 46.7)))
    if params.get("make_active", True):
        bpy.context.scene.camera = item
    return _apply_camera(item, params)


@command("set_camera")
def set_camera(params):
    item = _object(_require(params, "name"))
    if item.type != "CAMERA":
        raise CommandError("BadRequest", f"'{item.name}' is a {item.type}, not a camera")
    if params.get("make_active"):
        bpy.context.scene.camera = item
    return _apply_camera(item, params)


def _apply_camera(item, params):
    camera = item.data
    for key in ("lens", "sensor_width", "clip_start", "clip_end"):
        if params.get(key) is not None:
            setattr(camera, key, float(params[key]))
    if params.get("look_at") is not None:
        target = _vector(params, "look_at")
        direction = tuple(b - a for a, b in zip(item.location, target, strict=False))
        item.rotation_euler = mathutils.Vector(direction).to_track_quat("-Z", "Y").to_euler()
    dof = params.get("dof")
    if isinstance(dof, dict):
        camera.dof.use_dof = bool(dof.get("enabled", True))
        if dof.get("focus_distance") is not None:
            camera.dof.focus_distance = float(dof["focus_distance"])
        if dof.get("fstop") is not None:
            camera.dof.aperture_fstop = float(dof["fstop"])
        if dof.get("focus_object"):
            camera.dof.focus_object = _object(dof["focus_object"])
    return _camera_summary(item)


def _camera_summary(item):
    camera = item.data
    summary = _object_summary(item)
    summary.update({
        "active": bpy.context.scene.camera == item,
        "lens": camera.lens,
        "sensor_width": camera.sensor_width,
        "clip": [camera.clip_start, camera.clip_end],
        "dof": {
            "enabled": camera.dof.use_dof,
            "focus_distance": camera.dof.focus_distance,
            "fstop": camera.dof.aperture_fstop,
            "focus_object": _name(camera.dof.focus_object),
        },
    })
    summary["rotation_euler"] = _degrees(item.rotation_euler)
    return summary
