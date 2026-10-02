"""Objects: primitives, text and empties, one object in depth, selection, deletion, duplication, transforms and parenting."""

import contextlib
import math

import bpy

from .core import _name, _object, _require, command, selected_objects
from .operators import _operator
from .serialize import to_json
from .server import CommandError


@command("object_info")
def object_info(params):
    item = _object(_require(params, "name"))
    info = _object_summary(item)
    info.update({
        "dimensions": to_json(item.dimensions),
        "parent": _name(item.parent),
        "children": [child.name for child in item.children],
        "collections": [collection.name for collection in item.users_collection],
        "modifiers": [{"name": modifier.name, "type": modifier.type, "enabled": modifier.show_viewport} for modifier in item.modifiers],
        "materials": [_name(slot.material) for slot in item.material_slots],
        "constraints": [{"name": constraint.name, "type": constraint.type, "target": _name(getattr(constraint, "target", None))} for constraint in item.constraints],
        "shape_keys": [block.name for block in item.data.shape_keys.key_blocks] if getattr(item.data, "shape_keys", None) else [],
        "hidden": item.hide_get(),
        "hide_render": item.hide_render,
    })
    if item.type == "MESH":
        mesh = item.data
        info["mesh"] = {"vertices": len(mesh.vertices), "edges": len(mesh.edges), "faces": len(mesh.polygons)}
    return info


def _object_summary(item):
    return {
        "name": item.name,
        "type": item.type,
        "location": to_json(item.location),
        "rotation_euler": _degrees(item.rotation_euler),
        "scale": to_json(item.scale),
        "visible": item.visible_get(),
        "selected": item.select_get(),
    }


PRIMITIVES = {
    "cube": ("mesh.primitive_cube_add", {"size": "size"}),
    "plane": ("mesh.primitive_plane_add", {"size": "size"}),
    "grid": ("mesh.primitive_grid_add", {"size": "size"}),
    "monkey": ("mesh.primitive_monkey_add", {"size": "size"}),
    "circle": ("mesh.primitive_circle_add", {"radius": "size"}),
    "uv_sphere": ("mesh.primitive_uv_sphere_add", {"radius": "size"}),
    "ico_sphere": ("mesh.primitive_ico_sphere_add", {"radius": "size"}),
    "cylinder": ("mesh.primitive_cylinder_add", {"radius": "size", "depth": "depth"}),
    "cone": ("mesh.primitive_cone_add", {"radius1": "size", "depth": "depth"}),
    "torus": ("mesh.primitive_torus_add", {"major_radius": "size", "minor_radius": "minor_radius"}),
}


def _vector(params, key, default=(0.0, 0.0, 0.0)):
    value = params.get(key)
    if value is None:
        return default
    if not isinstance(value, (list, tuple)) or len(value) != 3:
        raise CommandError("BadRequest", f"'{key}' must be a list of three numbers")
    return tuple(float(item) for item in value)


def _radians(degrees):
    return tuple(math.radians(item) for item in degrees)


def _degrees(radians):
    return [round(math.degrees(item), 4) for item in radians]


def _ensure_object_mode():
    if bpy.context.mode != "OBJECT" and bpy.ops.object.mode_set.poll():
        bpy.ops.object.mode_set(mode="OBJECT")


def _window_override():
    windows = bpy.context.window_manager.windows
    if not windows:
        return contextlib.nullcontext()
    window = windows[0]
    return bpy.context.temp_override(window=window, screen=window.screen)


def _link_new_object(item):
    bpy.context.scene.collection.objects.link(item)
    bpy.context.view_layer.objects.active = item
    return item


@command("add_primitive")
def add_primitive(params):
    kind = _require(params, "kind")
    if kind not in PRIMITIVES:
        raise CommandError("BadRequest", f"unknown primitive '{kind}'", {"known": sorted(PRIMITIVES)})
    operator_name, sizing = PRIMITIVES[kind]
    arguments = {
        "location": _vector(params, "location"),
        "rotation": _radians(_vector(params, "rotation")),
    }
    for property_name, param_name in sizing.items():
        if params.get(param_name) is not None:
            arguments[property_name] = float(params[param_name])
    _ensure_object_mode()
    _operator(operator_name)(**arguments)
    item = bpy.context.view_layer.objects.active
    item.scale = _vector(params, "scale", (1.0, 1.0, 1.0))
    if params.get("name"):
        item.name = params["name"]
    return _object_summary(item)


@command("select_objects")
def select_objects(params):
    mode = params.get("mode") or "replace"
    if mode not in ("replace", "add", "none", "all"):
        raise CommandError("BadRequest", "'mode' must be replace, add, none or all")
    _ensure_object_mode()
    if mode in ("replace", "none", "all"):
        for item in bpy.context.view_layer.objects:
            item.select_set(mode == "all")
    if mode in ("replace", "add"):
        for item in _matching_objects(params):
            item.select_set(True)
            bpy.context.view_layer.objects.active = item
    return {
        "selected": [item.name for item in selected_objects()],
        "active": _name(bpy.context.view_layer.objects.active),
    }


def _matching_objects(params):
    names = params.get("names")
    if names:
        return [_object(name) for name in names]
    wanted_type = params.get("type")
    needle = (params.get("name_contains") or "").lower()
    return [
        item for item in bpy.context.view_layer.objects
        if (not wanted_type or item.type == wanted_type) and needle in item.name.lower()
    ]


@command("delete_objects")
def delete_objects(params):
    names = params.get("names") or []
    targets = [_object(name) for name in names]
    if params.get("selected"):
        targets.extend(item for item in selected_objects() if item not in targets)
    _ensure_object_mode()
    deleted = [item.name for item in targets]
    for item in targets:
        bpy.data.objects.remove(item, do_unlink=True)
    return {"deleted": deleted, "remaining": len(bpy.context.scene.objects)}


@command("duplicate_object")
def duplicate_object(params):
    source = _object(_require(params, "name"))
    copy = source.copy()
    if source.data is not None and not params.get("linked"):
        copy.data = source.data.copy()
    copy.animation_data_clear()
    for collection in source.users_collection:
        collection.objects.link(copy)
    if not copy.users_collection:
        bpy.context.scene.collection.objects.link(copy)
    copy.location = tuple(a + b for a, b in zip(source.location, _vector(params, "offset"), strict=False))
    if params.get("new_name"):
        copy.name = params["new_name"]
    bpy.context.view_layer.objects.active = copy
    return _object_summary(copy)


@command("transform_object")
def transform_object(params):
    item = _object(_require(params, "name"))
    relative = bool(params.get("relative"))
    if params.get("location") is not None:
        location = _vector(params, "location")
        item.location = tuple(a + b for a, b in zip(item.location, location, strict=False)) if relative else location
    if params.get("rotation") is not None:
        rotation = _radians(_vector(params, "rotation"))
        item.rotation_euler = tuple(a + b for a, b in zip(item.rotation_euler, rotation, strict=False)) if relative else rotation
    if params.get("scale") is not None:
        scale = _vector(params, "scale", (1.0, 1.0, 1.0))
        item.scale = tuple(a * b for a, b in zip(item.scale, scale, strict=False)) if relative else scale
    return _object_summary(item)


@command("update_object")
def update_object(params):
    item = _object(_require(params, "name"))
    if params.get("new_name"):
        item.name = params["new_name"]
    if params.get("hide_viewport") is not None:
        item.hide_set(bool(params["hide_viewport"]))
    if params.get("hide_render") is not None:
        item.hide_render = bool(params["hide_render"])
    if "parent" in params:
        _reparent(item, params.get("parent"), bool(params.get("keep_transform", True)))
    return object_info({"name": item.name})


def _reparent(item, parent_name, keep_transform):
    if not parent_name:
        matrix = item.matrix_world.copy()
        item.parent = None
        if keep_transform:
            item.matrix_world = matrix
        return
    parent = _object(parent_name)
    if parent == item:
        raise CommandError("BadRequest", "an object cannot be its own parent")
    item.parent = parent
    if keep_transform:
        item.matrix_parent_inverse = parent.matrix_world.inverted()


EMPTY_TYPES = ("PLAIN_AXES", "ARROWS", "SINGLE_ARROW", "CIRCLE", "CUBE", "SPHERE", "CONE", "IMAGE")


@command("add_text")
def add_text(params):
    name = params.get("name") or "Text"
    data = bpy.data.curves.new(name, "FONT")
    data.body = params.get("body") or "Text"
    if params.get("size") is not None:
        data.size = float(params["size"])
    if params.get("extrude") is not None:
        data.extrude = float(params["extrude"])
    if params.get("bevel_depth") is not None:
        data.bevel_depth = float(params["bevel_depth"])
    if params.get("align"):
        data.align_x = params["align"].upper()
    if params.get("font_path"):
        data.font = bpy.data.fonts.load(params["font_path"], check_existing=True)
    item = _link_new_object(bpy.data.objects.new(name, data))
    item.location = _vector(params, "location")
    item.rotation_euler = _radians(_vector(params, "rotation", (90.0, 0.0, 0.0) if params.get("upright", True) else (0.0, 0.0, 0.0)))
    summary = _object_summary(item)
    summary.update({"body": data.body, "size": data.size, "extrude": data.extrude, "align": data.align_x, "font": _name(data.font)})
    return summary


@command("add_empty")
def add_empty(params):
    kind = (params.get("type") or "PLAIN_AXES").upper()
    if kind not in EMPTY_TYPES:
        raise CommandError("BadRequest", f"unknown empty type '{kind}'", {"known": list(EMPTY_TYPES)})
    name = params.get("name") or "Empty"
    item = _link_new_object(bpy.data.objects.new(name, None))
    item.empty_display_type = kind
    if params.get("size") is not None:
        item.empty_display_size = float(params["size"])
    item.location = _vector(params, "location")
    item.rotation_euler = _radians(_vector(params, "rotation"))
    if kind == "IMAGE" and params.get("image_path"):
        item.data = bpy.data.images.load(params["image_path"], check_existing=True)
    if params.get("parent"):
        item.parent = _object(params["parent"])
    summary = _object_summary(item)
    summary.update({"display": item.empty_display_type, "size": item.empty_display_size, "parent": _name(item.parent)})
    return summary
