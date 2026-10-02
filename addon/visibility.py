"""Per-object rendering flags: holdout, shadow catcher, ray visibility, pass index, light group, caustics, motion blur."""

import bpy

from .core import _object, _require, command
from .server import CommandError

RAYS = ("camera", "diffuse", "glossy", "transmission", "volume_scatter", "shadow")


def _objects_named(names):
    targets = []
    for name in names:
        collection = bpy.data.collections.get(name)
        if collection is not None:
            targets.extend(collection.all_objects)
        else:
            targets.append(_object(name))
    seen = []
    for item in targets:
        if item not in seen:
            seen.append(item)
    return seen


def _flags(item):
    flags = {
        "name": item.name,
        "holdout": item.is_holdout,
        "shadow_catcher": item.is_shadow_catcher,
        "visible": {ray: getattr(item, f"visible_{ray}") for ray in RAYS},
        "pass_index": item.pass_index,
        "light_group": item.lightgroup or None,
    }
    if hasattr(item, "cycles"):
        cycles = item.cycles
        flags["caustics"] = {"caster": cycles.is_caustics_caster, "receiver": cycles.is_caustics_receiver}
        flags["motion_blur"] = {"enabled": cycles.use_motion_blur, "steps": cycles.motion_steps, "deform": cycles.use_deform_motion}
        flags["shadow_terminator_offset"] = cycles.shadow_terminator_offset
    return flags


@command("object_render_flags")
def object_render_flags(params):
    names = params.get("names") or [_require(params, "name")]
    targets = _objects_named(names)
    if not targets:
        raise CommandError("NotFound", "no objects matched", {"names": names})
    for item in targets:
        if params.get("holdout") is not None:
            item.is_holdout = bool(params["holdout"])
        if params.get("shadow_catcher") is not None:
            item.is_shadow_catcher = bool(params["shadow_catcher"])
        visible = params.get("visible")
        if isinstance(visible, dict):
            for ray, value in visible.items():
                if ray not in RAYS:
                    raise CommandError("BadRequest", f"unknown ray type '{ray}'", {"known": list(RAYS)})
                setattr(item, f"visible_{ray}", bool(value))
        if params.get("pass_index") is not None:
            item.pass_index = int(params["pass_index"])
        if params.get("light_group") is not None:
            item.lightgroup = params["light_group"]
        if hasattr(item, "cycles"):
            caustics = params.get("caustics")
            if isinstance(caustics, dict):
                if caustics.get("caster") is not None:
                    item.cycles.is_caustics_caster = bool(caustics["caster"])
                if caustics.get("receiver") is not None:
                    item.cycles.is_caustics_receiver = bool(caustics["receiver"])
            blur = params.get("motion_blur")
            if isinstance(blur, dict):
                if blur.get("enabled") is not None:
                    item.cycles.use_motion_blur = bool(blur["enabled"])
                if blur.get("steps") is not None:
                    item.cycles.motion_steps = max(1, min(7, int(blur["steps"])))
                if blur.get("deform") is not None:
                    item.cycles.use_deform_motion = bool(blur["deform"])
            if params.get("shadow_terminator_offset") is not None:
                item.cycles.shadow_terminator_offset = max(0.0, min(1.0, float(params["shadow_terminator_offset"])))
    return {"objects": [_flags(item) for item in targets], "count": len(targets)}
