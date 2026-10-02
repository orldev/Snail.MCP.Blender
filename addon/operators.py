"""The generic layer: any bpy operator with its documentation, and Python itself."""

import contextlib
import io
import math

import bpy

from .core import _name, _require, command
from .serialize import to_json
from .server import CommandError

STDOUT_LIMIT = 64 * 1024


@command("python")
def run_python(params):
    code = _require(params, "code")
    namespace = {"bpy": bpy, "math": math, "__name__": "__snail__"}
    _import_optional(namespace, "bmesh")
    _import_optional(namespace, "mathutils")
    captured = io.StringIO()
    with contextlib.redirect_stdout(captured):
        exec(compile(code, "<snail>", "exec"), namespace)
    return {
        "stdout": captured.getvalue()[-STDOUT_LIMIT:],
        "result": to_json(namespace.get("result")),
    }


def _import_optional(namespace, module_name):
    with contextlib.suppress(ImportError):
        namespace[module_name] = __import__(module_name)


@command("run_operator")
def run_operator(params):
    name = _require(params, "name")
    arguments = params.get("params") or {}
    if not isinstance(arguments, dict):
        raise CommandError("BadRequest", "'params' must be an object of operator properties")
    operator = _operator(name)
    rna = operator.get_rna_type()
    unknown = sorted(key for key in arguments if key not in rna.properties)
    if unknown:
        raise CommandError("UnknownParameter", f"bpy.ops.{name} has no property {', '.join(unknown)}",
                           {"known": _property_names(rna)})
    coerced = {key: _coerce(rna.properties[key], value) for key, value in arguments.items()}
    with _override(params.get("area")):
        if not operator.poll():
            raise CommandError("PollFailed", f"bpy.ops.{name} cannot run in the current context",
                               {"mode": bpy.context.mode, "active": _name(bpy.context.view_layer.objects.active)})
        status = operator(**coerced)
    return {"status": sorted(status), "active": _name(bpy.context.view_layer.objects.active)}


@command("describe_operator")
def describe_operator(params):
    name = _require(params, "name")
    rna = _operator(name).get_rna_type()
    return {
        "name": name,
        "label": rna.name,
        "description": rna.description,
        "properties": [_describe_property(prop) for prop in rna.properties if prop.identifier != "rna_type"],
    }


def _find_operator(name):
    module_name, _, operator_name = name.partition(".")
    if not operator_name:
        return None
    try:
        operator = getattr(getattr(bpy.ops, module_name), operator_name)
        operator.get_rna_type()
        return operator
    except (AttributeError, KeyError):
        return None


def _operator(name):
    operator = _find_operator(name)
    if operator is None:
        raise CommandError("UnknownOperator", f"bpy.ops.{name} does not exist")
    return operator


def _property_names(rna):
    return [prop.identifier for prop in rna.properties if prop.identifier != "rna_type"]


def _describe_property(prop):
    description = {
        "name": prop.identifier,
        "label": prop.name,
        "type": prop.type,
        "description": prop.description,
    }
    if prop.type == "ENUM":
        description["enum"] = [{"value": item.identifier, "label": item.name} for item in prop.enum_items]
        description["flag"] = prop.is_enum_flag
    if prop.type in ("INT", "FLOAT"):
        description["min"] = to_json(prop.hard_min)
        description["max"] = to_json(prop.hard_max)
    if getattr(prop, "is_array", False):
        description["length"] = prop.array_length
    if prop.type in ("BOOLEAN", "INT", "FLOAT", "STRING", "ENUM"):
        description["default"] = _default(prop)
    return description


def _default(prop):
    if getattr(prop, "is_array", False):
        return to_json(list(prop.default_array))
    if prop.type == "ENUM" and prop.is_enum_flag:
        return sorted(prop.default_flag)
    return to_json(prop.default)


def _coerce(prop, value):
    if prop.type == "ENUM":
        return set(value) if prop.is_enum_flag and isinstance(value, list) else value
    if prop.type in ("INT", "FLOAT") and getattr(prop, "is_array", False):
        return tuple(value)
    if prop.type == "BOOLEAN" and getattr(prop, "is_array", False):
        return tuple(bool(item) for item in value)
    return value


def _override(area_type):
    if not area_type:
        return contextlib.nullcontext()
    for window in bpy.context.window_manager.windows:
        for area in window.screen.areas:
            if area.type != area_type:
                continue
            region = next((region for region in area.regions if region.type == "WINDOW"), None)
            return bpy.context.temp_override(window=window, area=area, region=region)
    raise CommandError("NoArea", f"no open editor of type {area_type}; open one in Blender or drop the 'area' argument")
