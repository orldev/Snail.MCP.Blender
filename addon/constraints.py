"""Constraint commands on objects and pose bones."""

import bpy

from .core import _name, _object, _require, command
from .modeling import _apply_settings
from .operators import _describe_property
from .server import CommandError


def _holder(params):
    item = _object(_require(params, "name"))
    bone_name = params.get("bone")
    if not bone_name:
        return item, item
    if item.type != "ARMATURE":
        raise CommandError("BadRequest", f"'{item.name}' is a {item.type}; only armatures have bones")
    bone = item.pose.bones.get(bone_name)
    if bone is None:
        raise CommandError("NotFound", f"no bone named '{bone_name}'", {"bones": [bone.name for bone in item.pose.bones]})
    return item, bone


def _constraint_summary(constraint):
    settings = {}
    for prop in constraint.bl_rna.properties:
        if prop.identifier in ("rna_type", "name", "type") or prop.is_readonly:
            continue
        value = getattr(constraint, prop.identifier)
        settings[prop.identifier] = _name(value) if prop.type == "POINTER" else (list(value) if hasattr(value, "__len__") and not isinstance(value, str) else value)
    return {"name": constraint.name, "type": constraint.type, "enabled": constraint.enabled, "influence": constraint.influence, "settings": settings}


def _constraint_types():
    return [entry.identifier for entry in bpy.types.Constraint.bl_rna.properties["type"].enum_items]


def _find(holder, name):
    constraint = holder.constraints.get(name)
    if constraint is None:
        raise CommandError("NotFound", f"no constraint named '{name}'", {"constraints": [constraint.name for constraint in holder.constraints]})
    return constraint


@command("add_constraint")
def add_constraint(params):
    item, holder = _holder(params)
    kind = _require(params, "type").upper()
    if kind not in _constraint_types():
        raise CommandError("BadRequest", f"unknown constraint type '{kind}'", {"known": _constraint_types()})
    constraint = holder.constraints.new(kind)
    if params.get("constraint_name"):
        constraint.name = params["constraint_name"]
    _apply_settings(constraint, params.get("settings") or {})
    if params.get("influence") is not None:
        constraint.influence = float(params["influence"])
    summary = _constraint_summary(constraint)
    summary["owner"] = item.name
    summary["bone"] = params.get("bone")
    return summary


@command("update_constraint")
def update_constraint(params):
    _item, holder = _holder(params)
    constraint = _find(holder, _require(params, "constraint"))
    _apply_settings(constraint, params.get("settings") or {})
    if params.get("influence") is not None:
        constraint.influence = float(params["influence"])
    if params.get("enabled") is not None:
        constraint.enabled = bool(params["enabled"])
    if params.get("new_name"):
        constraint.name = params["new_name"]
    return _constraint_summary(constraint)


@command("remove_constraint")
def remove_constraint(params):
    item, holder = _holder(params)
    constraint = _find(holder, _require(params, "constraint"))
    removed = constraint.name
    holder.constraints.remove(constraint)
    return {"owner": item.name, "bone": params.get("bone"), "removed": removed, "constraints": [constraint.name for constraint in holder.constraints]}


@command("describe_constraint")
def describe_constraint(params):
    kind = params.get("type")
    if not kind:
        return {"types": _constraint_types()}
    wanted = f"{kind.replace('_', '').lower()}constraint"
    cls = next((getattr(bpy.types, name) for name in dir(bpy.types) if name.lower() == wanted), None)
    if cls is None:
        raise CommandError("BadRequest", f"unknown constraint type '{kind}'", {"known": _constraint_types()})
    return {
        "type": kind.upper(),
        "label": cls.bl_rna.name,
        "description": cls.bl_rna.description,
        "settings": [_describe_property(prop) for prop in cls.bl_rna.properties if prop.identifier not in ("rna_type", "name", "type") and not prop.is_readonly],
    }
