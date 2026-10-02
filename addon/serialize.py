"""Turns bpy and mathutils values into JSON-safe data without ever raising."""

import math

import bpy

COLLECTION_LIMIT = 200


def to_json(value, depth=0):
    if value is None or isinstance(value, (bool, int, str)):
        return value
    if isinstance(value, float):
        return value if math.isfinite(value) else None
    if isinstance(value, bytes):
        return value.decode("utf-8", errors="replace")
    if _is_mathutils(value):
        return _mathutils(value)
    if isinstance(value, bpy.types.ID):
        return {"name": value.name, "type": type(value).__name__}
    if isinstance(value, dict):
        return {str(key): to_json(item, depth + 1) for key, item in value.items()}
    if depth > 6:
        return str(value)
    if isinstance(value, (list, tuple, set, frozenset)) or type(value).__name__ == "bpy_prop_array":
        return [to_json(item, depth + 1) for item in list(value)[:COLLECTION_LIMIT]]
    if type(value).__name__ == "bpy_prop_collection":
        return [to_json(item, depth + 1) for item in list(value)[:COLLECTION_LIMIT]]
    if isinstance(value, bpy.types.bpy_struct):
        return _struct(value)
    return str(value)


def _is_mathutils(value):
    return type(value).__name__ in MATHUTILS_TYPES or type(value).__module__ == "mathutils"


MATHUTILS_TYPES = ("Vector", "Color", "Euler", "Quaternion", "Matrix")


def _mathutils(value):
    if hasattr(value, "to_tuple"):
        return list(value.to_tuple())
    if type(value).__name__ == "Matrix":
        return [list(row) for row in value]
    return list(value)


def _struct(value):
    description = {"type": type(value).__name__}
    name = getattr(value, "name", None)
    if isinstance(name, str):
        description["name"] = name
    return description
