"""Import and export in the formats Blender ships operators for."""

import os

import bpy

from .core import _require, command
from .objects import _window_override
from .operators import _find_operator
from .server import CommandError

IMPORTERS = {
    "fbx": ("import_scene.fbx", "filepath"), "obj": ("wm.obj_import", "filepath"), "stl": ("wm.stl_import", "filepath"),
    "ply": ("wm.ply_import", "filepath"), "gltf": ("import_scene.gltf", "filepath"), "glb": ("import_scene.gltf", "filepath"),
    "dae": ("wm.collada_import", "filepath"), "usd": ("wm.usd_import", "filepath"), "usda": ("wm.usd_import", "filepath"),
    "usdc": ("wm.usd_import", "filepath"), "usdz": ("wm.usd_import", "filepath"), "abc": ("wm.alembic_import", "filepath"),
    "x3d": ("import_scene.x3d", "filepath"), "svg": ("import_curve.svg", "filepath"), "dxf": ("import_scene.dxf", "filepath"),
    "3ds": ("import_scene.max3ds", "filepath"),
}


EXPORTERS = {
    "fbx": "export_scene.fbx", "obj": "wm.obj_export", "stl": "wm.stl_export", "ply": "wm.ply_export", "gltf": "export_scene.gltf",
    "glb": "export_scene.gltf", "dae": "wm.collada_export", "usd": "wm.usd_export", "usda": "wm.usd_export", "usdc": "wm.usd_export",
    "usdz": "wm.usd_export", "abc": "wm.alembic_export", "x3d": "export_scene.x3d", "dxf": "export.dxf", "3ds": "export_scene.max3ds",
}


def _format_of(path, explicit):
    extension = (explicit or os.path.splitext(path)[1].lstrip(".")).lower()
    if not extension:
        raise CommandError("BadRequest", "pass a format or a path with an extension")
    return extension


def _io_operator(table, extension, path):
    if extension not in table:
        raise CommandError("BadRequest", f"unsupported format '{extension}'", {"known": sorted(table)})
    entry = table[extension]
    name = entry[0] if isinstance(entry, tuple) else entry
    operator = _find_operator(name)
    if operator is None or not operator.poll():
        raise CommandError("Unsupported", f"this Blender has no working '{name}' operator for {extension}; enable the add-on in Preferences")
    return operator


@command("import_file")
def import_file(params):
    path = _require(params, "path")
    if not os.path.isfile(path):
        raise CommandError("NotFound", f"no file at '{path}'")
    extension = _format_of(path, params.get("format"))
    operator = _io_operator(IMPORTERS, extension, path)
    before = {item.name for item in bpy.data.objects}
    arguments = {"filepath": path}
    arguments.update(params.get("options") or {})
    with _window_override():
        operator(**arguments)
    imported = [item.name for item in bpy.data.objects if item.name not in before]
    return {"path": path, "format": extension, "imported": imported, "count": len(imported)}


@command("export_file")
def export_file(params):
    path = _require(params, "path")
    extension = _format_of(path, params.get("format"))
    operator = _io_operator(EXPORTERS, extension, path)
    directory = os.path.dirname(path)
    if directory:
        os.makedirs(directory, exist_ok=True)
    arguments = {"filepath": path}
    if params.get("selected_only"):
        arguments.update(_selection_flag(extension))
        if params.get("names"):
            for item in bpy.context.view_layer.objects:
                item.select_set(item.name in params["names"])
    if extension == "glb":
        arguments["export_format"] = "GLB"
    elif extension == "gltf":
        arguments["export_format"] = "GLTF_SEPARATE"
    arguments.update(params.get("options") or {})
    with _window_override():
        operator(**arguments)
    if not os.path.isfile(path):
        raise CommandError("ExportFailed", f"the exporter ran but no file appeared at '{path}'")
    return {"path": path, "format": extension, "bytes": os.path.getsize(path), "selected_only": bool(params.get("selected_only"))}


def _selection_flag(extension):
    if extension in ("fbx", "gltf", "glb", "dae", "usd", "usda", "usdc", "usdz", "abc", "x3d"):
        return {"use_selection": True}
    if extension in ("obj", "stl", "ply"):
        return {"export_selected_objects": True}
    return {}
