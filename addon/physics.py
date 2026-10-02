"""Physics commands: rigid bodies, cloth, soft bodies, collisions, fluids, particles and cache baking."""

import bpy

from .core import _object, _require, command
from .modeling import _apply_settings, _object_override
from .server import CommandError

MODIFIER_PHYSICS = {
    "cloth": ("CLOTH", "settings"),
    "soft_body": ("SOFT_BODY", "settings"),
    "collision": ("COLLISION", "settings"),
    "fluid_domain": ("FLUID", "domain_settings"),
    "fluid_flow": ("FLUID", "flow_settings"),
    "fluid_effector": ("FLUID", "effector_settings"),
    "dynamic_paint": ("DYNAMIC_PAINT", None),
}
PHYSICS_KINDS = ("rigid_body", "rigid_body_passive", *MODIFIER_PHYSICS)
PARTICLE_KINDS = ("EMITTER", "HAIR")


def _rigid_body_summary(item):
    body = item.rigid_body
    return {
        "type": body.type,
        "mass": body.mass,
        "friction": body.friction,
        "restitution": body.restitution,
        "collision_shape": body.collision_shape,
        "kinematic": body.kinematic,
    }


def _physics_summary(item):
    summary = {"name": item.name, "rigid_body": _rigid_body_summary(item) if item.rigid_body else None, "modifiers": []}
    for modifier in item.modifiers:
        if modifier.type in ("CLOTH", "SOFT_BODY", "COLLISION", "FLUID", "DYNAMIC_PAINT", "PARTICLE_SYSTEM"):
            entry = {"name": modifier.name, "type": modifier.type}
            if modifier.type == "FLUID":
                entry["fluid_type"] = modifier.fluid_type
            summary["modifiers"].append(entry)
    summary["particle_systems"] = [{"name": system.name, "type": system.settings.type, "count": system.settings.count} for system in item.particle_systems]
    return summary


@command("add_physics")
def add_physics(params):
    item = _object(_require(params, "name"))
    kind = _require(params, "type")
    if kind not in PHYSICS_KINDS:
        raise CommandError("BadRequest", f"unknown physics type '{kind}'", {"known": list(PHYSICS_KINDS)})
    settings = params.get("settings") or {}
    if kind.startswith("rigid_body"):
        if item.type != "MESH":
            raise CommandError("BadRequest", f"'{item.name}' is a {item.type}; rigid bodies need a mesh")
        with _object_override([item], item):
            if item.rigid_body is None:
                bpy.ops.rigidbody.object_add(type="PASSIVE" if kind == "rigid_body_passive" else "ACTIVE")
            else:
                item.rigid_body.type = "PASSIVE" if kind == "rigid_body_passive" else "ACTIVE"
        _apply_settings(item.rigid_body, settings)
        return _physics_summary(item)
    modifier_type, settings_attribute = MODIFIER_PHYSICS[kind]
    modifier = next((modifier for modifier in item.modifiers if modifier.type == modifier_type), None)
    if modifier is None:
        try:
            modifier = item.modifiers.new(name=kind.replace("_", " ").title(), type=modifier_type)
        except RuntimeError as error:
            raise CommandError("BadRequest", f"'{item.name}' ({item.type}) cannot take {kind}: {error}") from error
    if kind.startswith("fluid_"):
        modifier.fluid_type = {"fluid_domain": "DOMAIN", "fluid_flow": "FLOW", "fluid_effector": "EFFECTOR"}[kind]
    target = getattr(modifier, settings_attribute) if settings_attribute else modifier
    if target is None:
        raise CommandError("Unsupported", f"{kind} has no settings block on '{item.name}'")
    _apply_settings(target, settings)
    return _physics_summary(item)


@command("remove_physics")
def remove_physics(params):
    item = _object(_require(params, "name"))
    kind = _require(params, "type")
    removed = []
    if kind.startswith("rigid_body"):
        if item.rigid_body is not None:
            with _object_override([item], item):
                bpy.ops.rigidbody.object_remove()
            removed.append("rigid_body")
    elif kind == "particles":
        for modifier in [modifier for modifier in item.modifiers if modifier.type == "PARTICLE_SYSTEM"]:
            removed.append(modifier.name)
            item.modifiers.remove(modifier)
    elif kind in MODIFIER_PHYSICS:
        modifier_type = MODIFIER_PHYSICS[kind][0]
        for modifier in [modifier for modifier in item.modifiers if modifier.type == modifier_type]:
            removed.append(modifier.name)
            item.modifiers.remove(modifier)
    else:
        raise CommandError("BadRequest", f"unknown physics type '{kind}'", {"known": [*PHYSICS_KINDS, "particles"]})
    summary = _physics_summary(item)
    summary["removed"] = removed
    return summary


@command("add_particles")
def add_particles(params):
    item = _object(_require(params, "name"))
    kind = (params.get("type") or "EMITTER").upper()
    if kind not in PARTICLE_KINDS:
        raise CommandError("BadRequest", "type must be EMITTER or HAIR")
    if item.type != "MESH":
        raise CommandError("BadRequest", f"'{item.name}' is a {item.type}; particles need a mesh")
    modifier = item.modifiers.new(name=params.get("system_name") or ("Hair" if kind == "HAIR" else "Particles"), type="PARTICLE_SYSTEM")
    system = item.particle_systems[modifier.name] if modifier.name in item.particle_systems else item.particle_systems[-1]
    settings = system.settings
    settings.type = kind
    if params.get("count") is not None:
        settings.count = int(params["count"])
    if kind == "HAIR" and params.get("hair_length") is not None:
        settings.hair_length = float(params["hair_length"])
    if params.get("frame_start") is not None:
        settings.frame_start = float(params["frame_start"])
    if params.get("frame_end") is not None:
        settings.frame_end = float(params["frame_end"])
    if params.get("lifetime") is not None:
        settings.lifetime = float(params["lifetime"])
    _apply_settings(settings, params.get("settings") or {})
    return {
        "name": item.name,
        "system": system.name,
        "settings": settings.name,
        "type": settings.type,
        "count": settings.count,
        "frame_start": settings.frame_start,
        "frame_end": settings.frame_end,
        "lifetime": settings.lifetime,
        "hair_length": settings.hair_length,
        "render_type": settings.render_type,
    }


@command("bake_physics")
def bake_physics(params):
    scene = bpy.context.scene
    if params.get("frame_end") is not None:
        scene.frame_end = int(params["frame_end"])
    if scene.rigidbody_world is not None:
        scene.rigidbody_world.point_cache.frame_end = scene.frame_end
    windows = bpy.context.window_manager.windows
    overrides = {"scene": scene}
    if windows:
        overrides.update({"window": windows[0], "screen": windows[0].screen})
    with bpy.context.temp_override(**overrides):
        if params.get("free"):
            bpy.ops.ptcache.free_bake_all()
            return {"freed": True, "frames": [scene.frame_start, scene.frame_end]}
        bpy.ops.ptcache.bake_all(bake=True)
    return {"baked": True, "frames": [scene.frame_start, scene.frame_end], "rigid_body_world": scene.rigidbody_world is not None}
