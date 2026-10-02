"""Camera moves as animation presets: turntable, orbit, dolly; and three-point light rigs around a subject."""

import math

import bpy
import mathutils

from .cameras import _camera_of
from .core import _object, _require, _scene, command
from .lights import _blackbody
from .server import CommandError

MOVES = ("turntable", "orbit", "dolly", "push_in", "crane")
LIGHT_RIG_ROLES = ("key", "fill", "rim")


def _bounds(items):
    corners = [item.matrix_world @ mathutils.Vector(corner) for item in items for corner in item.bound_box]
    if not corners:
        raise CommandError("BadRequest", "the target has no geometry to frame")
    center = sum(corners, mathutils.Vector()) / len(corners)
    radius = max((corner - center).length for corner in corners) or 1.0
    return center, radius


def _subjects(params):
    names = params.get("targets") or ([params["target"]] if params.get("target") else [])
    items = [_object(name) for name in names]
    if not items:
        raise CommandError("BadRequest", "pass target or targets")
    return items


def _clear_keys(item):
    if item.animation_data:
        item.animation_data_clear()


def _aim(camera, target):
    for constraint in [constraint for constraint in camera.constraints if constraint.name == "Snail Aim"]:
        camera.constraints.remove(constraint)
    constraint = camera.constraints.new("TRACK_TO")
    constraint.name = "Snail Aim"
    constraint.target = target
    constraint.track_axis = "TRACK_NEGATIVE_Z"
    constraint.up_axis = "UP_Y"


def _pivot(scene, name, location):
    pivot = bpy.data.objects.get(name)
    if pivot is None:
        pivot = bpy.data.objects.new(name, None)
        pivot.empty_display_type = "PLAIN_AXES"
        scene.collection.objects.link(pivot)
    pivot.location = location
    return pivot


def _key(item, path, frame, interpolation="LINEAR"):
    item.keyframe_insert(data_path=path, frame=frame)
    for curve in item.animation_data.action.fcurves if item.animation_data and item.animation_data.action and hasattr(item.animation_data.action, "fcurves") else []:
        for point in curve.keyframe_points:
            point.interpolation = interpolation


@command("camera_move")
def camera_move(params):
    scene = _scene(params)
    kind = _require(params, "type").lower()
    if kind not in MOVES:
        raise CommandError("BadRequest", f"unknown move '{kind}'", {"known": list(MOVES)})
    targets = _subjects(params)
    center, radius = _bounds(targets)
    start = int(params.get("start") if params.get("start") is not None else scene.frame_start)
    end = int(params.get("end") if params.get("end") is not None else scene.frame_end)
    if end <= start:
        raise CommandError("BadRequest", "end must be after start")
    revolutions = float(params.get("revolutions") or 1.0)
    interpolation = (params.get("interpolation") or "LINEAR").upper()
    if kind == "turntable":
        pivot = _pivot(scene, params.get("pivot_name") or "Turntable", center.copy())
        pivot.location.z = min(corner.z for item in targets for corner in [item.matrix_world @ mathutils.Vector(vertex) for vertex in item.bound_box])
        _clear_keys(pivot)
        for item in targets:
            if item.parent is None:
                world = item.matrix_world.copy()
                item.parent = pivot
                item.matrix_parent_inverse = pivot.matrix_world.inverted()
                item.matrix_world = world
        pivot.rotation_euler = (0.0, 0.0, math.radians(float(params.get("start_angle") or 0.0)))
        _key(pivot, "rotation_euler", start, interpolation)
        pivot.rotation_euler.z += math.tau * revolutions
        _key(pivot, "rotation_euler", end, interpolation)
        return {"type": kind, "pivot": pivot.name, "frames": [start, end], "revolutions": revolutions, "objects": [item.name for item in targets]}
    camera = _camera_of(params)
    _clear_keys(camera)
    distance = float(params.get("distance") or radius * 2.5)
    height = float(params.get("height") if params.get("height") is not None else radius * 0.6)
    if kind == "orbit":
        pivot = _pivot(scene, params.get("pivot_name") or "Orbit", center.copy())
        _clear_keys(pivot)
        camera.parent = pivot
        camera.matrix_parent_inverse = mathutils.Matrix.Identity(4)
        camera.location = (distance, 0.0, height)
        _aim(camera, pivot)
        pivot.rotation_euler = (0.0, 0.0, math.radians(float(params.get("start_angle") or 0.0)))
        _key(pivot, "rotation_euler", start, interpolation)
        pivot.rotation_euler.z += math.tau * revolutions
        _key(pivot, "rotation_euler", end, interpolation)
        return {"type": kind, "pivot": pivot.name, "camera": camera.name, "frames": [start, end], "distance": distance, "height": height}
    azimuth = math.radians(float(params.get("start_angle") or 0.0))
    direction = mathutils.Vector((math.cos(azimuth), math.sin(azimuth), 0.0))
    far = float(params.get("distance") or radius * 3.0)
    near = float(params.get("distance_end") if params.get("distance_end") is not None else radius * 1.5)
    if kind in ("dolly", "push_in"):
        camera.parent = None
        _aim(camera, targets[0])
        camera.location = center + direction * far + mathutils.Vector((0.0, 0.0, height))
        _key(camera, "location", start, interpolation)
        camera.location = center + direction * near + mathutils.Vector((0.0, 0.0, height))
        _key(camera, "location", end, interpolation)
        return {"type": kind, "camera": camera.name, "frames": [start, end], "from_distance": far, "to_distance": near}
    camera.parent = None
    _aim(camera, targets[0])
    low = float(params.get("height") if params.get("height") is not None else 0.2 * radius)
    high = float(params.get("height_end") if params.get("height_end") is not None else radius * 2.5)
    camera.location = center + direction * far + mathutils.Vector((0.0, 0.0, low))
    _key(camera, "location", start, interpolation)
    camera.location = center + direction * far + mathutils.Vector((0.0, 0.0, high))
    _key(camera, "location", end, interpolation)
    return {"type": kind, "camera": camera.name, "frames": [start, end], "from_height": low, "to_height": high}


@command("light_rig")
def light_rig(params):
    scene = _scene(params)
    targets = _subjects(params)
    center, radius = _bounds(targets)
    distance = float(params.get("distance") or radius * 3.0)
    key_energy = float(params.get("key_energy") or 1000.0)
    fill_ratio = float(params.get("fill_ratio") if params.get("fill_ratio") is not None else 0.35)
    rim_ratio = float(params.get("rim_ratio") if params.get("rim_ratio") is not None else 1.5)
    key_azimuth = math.radians(float(params.get("key_angle") if params.get("key_angle") is not None else 45.0))
    prefix = params.get("name") or "Rig"
    roles = {
        "key": (key_azimuth, math.radians(35.0), key_energy, float(params.get("key_temperature") or 5600.0), radius * 1.2),
        "fill": (key_azimuth - math.radians(110.0), math.radians(15.0), key_energy * fill_ratio, float(params.get("fill_temperature") or 6500.0), radius * 2.0),
        "rim": (key_azimuth + math.radians(160.0), math.radians(45.0), key_energy * rim_ratio, float(params.get("rim_temperature") or 6500.0), radius * 0.8),
    }
    created = []
    for role in params.get("roles") or LIGHT_RIG_ROLES:
        if role not in roles:
            raise CommandError("BadRequest", f"unknown rig role '{role}'", {"known": list(LIGHT_RIG_ROLES)})
        azimuth, elevation, energy, kelvin, size = roles[role]
        name = f"{prefix} {role.title()}"
        existing = bpy.data.objects.get(name)
        if existing is not None:
            bpy.data.objects.remove(existing, do_unlink=True)
        data = bpy.data.lights.new(name, "AREA")
        data.energy = energy
        data.color = _blackbody(kelvin)
        data.size = size
        data.shape = "SQUARE"
        light = bpy.data.objects.new(name, data)
        scene.collection.objects.link(light)
        direction = mathutils.Vector((math.cos(elevation) * math.cos(azimuth), math.cos(elevation) * math.sin(azimuth), math.sin(elevation)))
        light.location = center + direction * distance
        constraint = light.constraints.new("TRACK_TO")
        constraint.target = targets[0]
        constraint.track_axis = "TRACK_NEGATIVE_Z"
        constraint.up_axis = "UP_Y"
        created.append({"name": name, "role": role, "energy": energy, "kelvin": kelvin, "size": round(size, 3), "location": [round(value, 3) for value in light.location]})
    return {"lights": created, "target": targets[0].name, "distance": distance}
