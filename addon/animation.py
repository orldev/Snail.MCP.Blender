"""Animation commands: keyframes and their curves, armatures and poses, weights, paths and drivers."""


import bpy

from .core import _name, _object, _require, command
from .modeling import _apply_settings, _object_override
from .objects import _degrees, _ensure_object_mode, _link_new_object, _radians, _vector
from .serialize import to_json
from .server import CommandError

ROTATION_PATHS = ("rotation_euler", "delta_rotation_euler")
INTERPOLATIONS = ("CONSTANT", "LINEAR", "BEZIER", "SINE", "QUAD", "CUBIC", "QUART", "QUINT", "EXPO", "CIRC", "BACK", "BOUNCE", "ELASTIC")
FCURVE_MODIFIERS = ("CYCLES", "NOISE", "ENVELOPE", "LIMITS", "STEPPED", "GENERATOR", "FNGENERATOR")
PARENT_METHODS = {"automatic": "ARMATURE_AUTO", "envelope": "ARMATURE_ENVELOPE", "empty": "ARMATURE"}


def _target(item, data_path):
    if data_path.startswith("data."):
        return item.data, data_path[len("data."):]
    return item, data_path


def _fcurves(item):
    animation = item.animation_data
    if animation is None or animation.action is None:
        return []
    action = animation.action
    curves = list(getattr(action, "fcurves", []))
    if curves:
        return curves
    for layer in getattr(action, "layers", []):
        for strip in layer.strips:
            for bag in getattr(strip, "channelbags", []):
                curves.extend(bag.fcurves)
    return curves


def _curve_summary(curve):
    keys = [{"frame": point.co.x, "value": point.co.y, "interpolation": point.interpolation} for point in curve.keyframe_points]
    return {
        "data_path": curve.data_path,
        "index": curve.array_index,
        "keyframes": keys[:200],
        "total": len(curve.keyframe_points),
        "modifiers": [modifier.type for modifier in curve.modifiers],
    }


def _chosen_curves(item, params):
    channels = params.get("channels")
    curves = _fcurves(item)
    if channels:
        curves = [curve for curve in curves if curve.data_path in channels or curve.data_path.split(".")[-1] in channels]
    return curves


def _in_range(point, params):
    start, end = params.get("from_frame"), params.get("to_frame")
    return (start is None or point.co.x >= float(start)) and (end is None or point.co.x <= float(end))


@command("set_frame")
def set_frame(params):
    scene = bpy.context.scene
    if params.get("start") is not None:
        scene.frame_start = int(params["start"])
    if params.get("end") is not None:
        scene.frame_end = int(params["end"])
    if params.get("fps") is not None:
        scene.render.fps = int(params["fps"])
    if params.get("frame") is not None:
        scene.frame_set(int(params["frame"]))
    return {"current": scene.frame_current, "start": scene.frame_start, "end": scene.frame_end, "fps": scene.render.fps}


@command("insert_keyframe")
def insert_keyframe(params):
    item = _object(_require(params, "name"))
    frame = int(params.get("frame") if params.get("frame") is not None else bpy.context.scene.frame_current)
    values = params.get("values") or {}
    channels = params.get("channels") or list(values) or ["location", "rotation_euler", "scale"]
    for path, value in values.items():
        target, attribute = _target(item, path)
        if path in ROTATION_PATHS:
            value = _radians(tuple(float(component) for component in value))
        elif isinstance(value, list):
            value = tuple(value)
        setattr(target, attribute, value)
    keyed = []
    for path in channels:
        target, attribute = _target(item, path)
        try:
            target.keyframe_insert(data_path=attribute, frame=frame)
        except (TypeError, RuntimeError) as error:
            raise CommandError("BadRequest", f"cannot key '{path}' on '{item.name}': {error}") from error
        keyed.append(path)
    if params.get("interpolation"):
        _set_interpolation(item, {"channels": channels, "interpolation": params["interpolation"], "from_frame": frame, "to_frame": frame})
    return {"name": item.name, "frame": frame, "channels": keyed, "keyframes": sum(len(curve.keyframe_points) for curve in _fcurves(item))}


@command("delete_keyframes")
def delete_keyframes(params):
    item = _object(_require(params, "name"))
    if params.get("frame") is None and not params.get("channels"):
        item.animation_data_clear()
        return {"name": item.name, "cleared": True}
    removed = 0
    for curve in _chosen_curves(item, params):
        points = curve.keyframe_points
        for index in reversed(range(len(points))):  # from the end: removing a key moves those after it, and a list taken first goes stale
            if params.get("frame") is None or int(points[index].co.x) == int(params["frame"]):
                points.remove(points[index])
                removed += 1
    return {"name": item.name, "removed": removed, "keyframes": sum(len(curve.keyframe_points) for curve in _fcurves(item))}


@command("list_keyframes")
def list_keyframes(params):
    item = _object(_require(params, "name"))
    animation = item.animation_data
    return {
        "name": item.name,
        "action": _name(animation.action) if animation else None,
        "curves": [_curve_summary(curve) for curve in _chosen_curves(item, params)],
        "drivers": [{"data_path": driver.data_path, "index": driver.array_index, "expression": driver.driver.expression} for driver in (animation.drivers if animation else [])],
    }


@command("set_interpolation")
def set_interpolation(params):
    item = _object(_require(params, "name"))
    return _set_interpolation(item, params)


def _set_interpolation(item, params):
    interpolation = (params.get("interpolation") or "BEZIER").upper()
    if interpolation not in INTERPOLATIONS:
        raise CommandError("BadRequest", f"unknown interpolation '{interpolation}'", {"known": list(INTERPOLATIONS)})
    easing = (params.get("easing") or "").upper() or None
    changed = 0
    for curve in _chosen_curves(item, params):
        for point in curve.keyframe_points:
            if not _in_range(point, params):
                continue
            point.interpolation = interpolation
            if easing:
                point.easing = easing
            changed += 1
        curve.update()
    return {"name": item.name, "interpolation": interpolation, "easing": easing, "keyframes": changed}


@command("move_keyframes")
def move_keyframes(params):
    item = _object(_require(params, "name"))
    offset = float(params.get("offset") or 0.0)
    factor = float(params.get("scale") or 1.0)
    pivot = float(params.get("pivot") if params.get("pivot") is not None else bpy.context.scene.frame_start)
    moved = 0
    for curve in _chosen_curves(item, params):
        for point in curve.keyframe_points:
            if not _in_range(point, params):
                continue
            for attribute in ("co", "handle_left", "handle_right"):
                vector = getattr(point, attribute)
                vector.x = pivot + (vector.x - pivot) * factor + offset
            moved += 1
        curve.update()
    return {"name": item.name, "moved": moved, "offset": offset, "scale": factor}


@command("add_fcurve_modifier")
def add_fcurve_modifier(params):
    item = _object(_require(params, "name"))
    kind = _require(params, "type").upper()
    if kind not in FCURVE_MODIFIERS:
        raise CommandError("BadRequest", f"unknown F-Curve modifier '{kind}'", {"known": list(FCURVE_MODIFIERS)})
    curves = _chosen_curves(item, params)
    if not curves:
        raise CommandError("NotFound", f"'{item.name}' has no animation curves to modify")
    for curve in curves:
        modifier = curve.modifiers.new(kind)
        _apply_settings(modifier, params.get("settings") or {})
    return {"name": item.name, "type": kind, "curves": [f"{curve.data_path}[{curve.array_index}]" for curve in curves]}


@command("add_armature")
def add_armature(params):
    name = params.get("name") or "Armature"
    data = bpy.data.armatures.new(name)
    item = _link_new_object(bpy.data.objects.new(name, data))
    item.location = _vector(params, "location")
    _ensure_object_mode()
    with _object_override([item], item):
        bpy.ops.object.mode_set(mode="EDIT")
        try:
            for spec in params.get("bones") or [{"name": "Bone", "head": [0, 0, 0], "tail": [0, 0, 1]}]:
                bone = data.edit_bones.new(spec.get("name") or "Bone")
                bone.head = tuple(spec.get("head") or (0, 0, 0))
                bone.tail = tuple(spec.get("tail") or (0, 0, 1))
                if spec.get("parent"):
                    bone.parent = data.edit_bones[spec["parent"]]
                    bone.use_connect = bool(spec.get("connected"))
        finally:
            bpy.ops.object.mode_set(mode="OBJECT")
    return _armature_summary(item)


def _armature_summary(item):
    return {
        "name": item.name,
        "bones": [{"name": bone.name, "head": to_json(bone.head_local), "tail": to_json(bone.tail_local), "parent": _name(bone.parent)} for bone in item.data.bones],
        "children": [child.name for child in item.children],
    }


@command("parent_to_armature")
def parent_to_armature(params):
    item = _object(_require(params, "name"))
    armature = _object(_require(params, "armature"))
    if armature.type != "ARMATURE":
        raise CommandError("BadRequest", f"'{armature.name}' is a {armature.type}, not an armature")
    method = params.get("method") or "automatic"
    if method not in PARENT_METHODS:
        raise CommandError("BadRequest", "method must be automatic, envelope or empty")
    _ensure_object_mode()
    with _object_override([item, armature], armature):
        bpy.ops.object.parent_set(type=PARENT_METHODS[method])
    return {"name": item.name, "armature": armature.name, "method": method, "vertex_groups": [group.name for group in item.vertex_groups]}


@command("pose_bone")
def pose_bone(params):
    armature = _object(_require(params, "armature"))
    if armature.type != "ARMATURE":
        raise CommandError("BadRequest", f"'{armature.name}' is a {armature.type}, not an armature")
    bone = armature.pose.bones.get(_require(params, "bone"))
    if bone is None:
        raise CommandError("NotFound", f"no bone named '{params['bone']}'", {"bones": [bone.name for bone in armature.pose.bones]})
    if params.get("location") is not None:
        bone.location = _vector(params, "location")
    if params.get("rotation") is not None:
        bone.rotation_mode = "XYZ"
        bone.rotation_euler = _radians(_vector(params, "rotation"))
    if params.get("scale") is not None:
        bone.scale = _vector(params, "scale", (1.0, 1.0, 1.0))
    keyed = []
    if params.get("frame") is not None:
        frame = int(params["frame"])
        for key, path in (("location", "location"), ("rotation", "rotation_euler"), ("scale", "scale")):
            if params.get(key) is not None:
                bone.keyframe_insert(data_path=path, frame=frame)
                keyed.append(path)
    return {"armature": armature.name, "bone": bone.name, "location": to_json(bone.location), "rotation": _degrees(bone.rotation_euler), "scale": to_json(bone.scale), "keyed": keyed}


@command("set_vertex_weights")
def set_vertex_weights(params):
    item = _object(_require(params, "name"))
    if item.type != "MESH":
        raise CommandError("BadRequest", f"'{item.name}' is a {item.type}, not a mesh")
    group = item.vertex_groups.get(_require(params, "group")) or item.vertex_groups.new(name=params["group"])
    weights = params.get("weights") or []
    if params.get("all") is not None:
        weights = [[vertex.index, float(params["all"])] for vertex in item.data.vertices]
    if params.get("selected") is not None:
        weights = [[vertex.index, float(params["selected"])] for vertex in item.data.vertices if vertex.select]
    for index, weight in weights:
        group.add([int(index)], float(weight), params.get("mode") or "REPLACE")
    return {"name": item.name, "group": group.name, "assigned": len(weights), "groups": [group.name for group in item.vertex_groups]}


@command("follow_path")
def follow_path(params):
    item = _object(_require(params, "name"))
    curve = _object(_require(params, "curve"))
    if curve.type != "CURVE":
        raise CommandError("BadRequest", f"'{curve.name}' is a {curve.type}, not a curve")
    constraint = next((constraint for constraint in item.constraints if constraint.type == "FOLLOW_PATH" and constraint.target == curve), None)
    if constraint is None:
        constraint = item.constraints.new("FOLLOW_PATH")
        constraint.target = curve
    constraint.use_curve_follow = bool(params.get("follow", True))
    if params.get("forward_axis"):
        constraint.forward_axis = params["forward_axis"]
    start = int(params.get("start") if params.get("start") is not None else bpy.context.scene.frame_start)
    duration = int(params.get("duration") or (bpy.context.scene.frame_end - start))
    curve.data.use_path = True
    curve.data.path_duration = max(1, duration)
    curve.data.eval_time = 0
    curve.data.keyframe_insert(data_path="eval_time", frame=start)
    curve.data.eval_time = duration
    curve.data.keyframe_insert(data_path="eval_time", frame=start + duration)
    for fcurve in _fcurves(curve.data) if hasattr(curve.data, "animation_data") else []:
        for point in fcurve.keyframe_points:
            point.interpolation = "LINEAR"
    return {"name": item.name, "curve": curve.name, "start": start, "duration": duration, "constraint": constraint.name}


@command("add_driver")
def add_driver(params):
    item = _object(_require(params, "name"))
    target, data_path = _target(item, _require(params, "data_path"))
    index = params.get("index")
    try:
        fcurve = target.driver_add(data_path, int(index)) if index is not None else target.driver_add(data_path)
    except (TypeError, RuntimeError) as error:
        raise CommandError("BadRequest", f"cannot drive '{params['data_path']}' on '{item.name}': {error}") from error
    driver = fcurve.driver
    driver.type = "SCRIPTED"
    driver.expression = params.get("expression") or "frame"
    for spec in params.get("variables") or []:
        variable = driver.variables.new()
        variable.name = spec.get("name") or "var"
        variable.type = spec.get("type") or "SINGLE_PROP"
        source = _object(spec["object"]) if spec.get("object") else item
        variable.targets[0].id = source
        if variable.type == "SINGLE_PROP":
            variable.targets[0].data_path = spec.get("data_path") or "location[0]"
        elif variable.type == "TRANSFORMS":
            variable.targets[0].transform_type = spec.get("transform_type") or "LOC_X"
            variable.targets[0].transform_space = spec.get("transform_space") or "WORLD_SPACE"
    return {"name": item.name, "data_path": params["data_path"], "index": index, "expression": driver.expression, "variables": [variable.name for variable in driver.variables]}


@command("add_shape_key")
def add_shape_key(params):
    item = _object(_require(params, "name"))
    if item.type not in ("MESH", "CURVE", "LATTICE"):
        raise CommandError("BadRequest", f"'{item.name}' is a {item.type}; shape keys need a mesh, curve or lattice")
    if item.data.shape_keys is None:
        item.shape_key_add(name="Basis", from_mix=False)
    key_name = params.get("key_name") or "Key"
    key = item.shape_key_add(name=key_name, from_mix=bool(params.get("from_mix")))
    for entry in params.get("vertices") or []:
        if not isinstance(entry, list) or len(entry) != 4:
            raise CommandError("BadRequest", "each entry is [index, x, y, z]")
        index = int(entry[0])
        if index < 0 or index >= len(key.data):
            raise CommandError("BadRequest", f"vertex index {index} is out of range (0..{len(key.data) - 1})")
        point = key.data[index]
        offset = tuple(float(component) for component in entry[1:])
        point.co = tuple(a + b for a, b in zip(point.co, offset, strict=False)) if params.get("relative", True) else offset
    if params.get("value") is not None:
        key.value = float(params["value"])
    return _shape_keys_summary(item)


@command("set_shape_key")
def set_shape_key(params):
    item = _object(_require(params, "name"))
    keys = item.data.shape_keys
    if keys is None:
        raise CommandError("NotFound", f"'{item.name}' has no shape keys")
    key = keys.key_blocks.get(_require(params, "key_name"))
    if key is None:
        raise CommandError("NotFound", f"no shape key named '{params['key_name']}'", {"keys": [block.name for block in keys.key_blocks]})
    if params.get("value") is not None:
        key.value = float(params["value"])
    if params.get("slider_min") is not None:
        key.slider_min = float(params["slider_min"])
    if params.get("slider_max") is not None:
        key.slider_max = float(params["slider_max"])
    if params.get("frame") is not None:
        key.keyframe_insert(data_path="value", frame=int(params["frame"]))
    return _shape_keys_summary(item)


def _shape_keys_summary(item):
    keys = item.data.shape_keys
    return {
        "name": item.name,
        "keys": [{"name": block.name, "value": block.value, "min": block.slider_min, "max": block.slider_max} for block in keys.key_blocks] if keys else [],
    }


@command("add_marker")
def add_marker(params):
    scene = bpy.context.scene
    frame = int(params.get("frame") if params.get("frame") is not None else scene.frame_current)
    marker = scene.timeline_markers.new(params.get("marker_name") or f"F_{frame}", frame=frame)
    if params.get("camera"):
        camera = _object(params["camera"])
        if camera.type != "CAMERA":
            raise CommandError("BadRequest", f"'{camera.name}' is not a camera")
        marker.camera = camera
    return {"markers": [{"name": marker.name, "frame": marker.frame, "camera": _name(marker.camera)} for marker in scene.timeline_markers]}
