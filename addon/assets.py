"""Assets from other .blend files: what a file holds, and appending or linking objects, collections, materials, node groups, worlds and images."""

import os

import bpy

from .core import _require, _scene, command
from .server import CommandError

ASSET_TYPES = {"objects": "objects", "collections": "collections", "materials": "materials", "node_groups": "node_groups", "worlds": "worlds",
               "images": "images", "meshes": "meshes", "actions": "actions", "cameras": "cameras", "lights": "lights", "scenes": "scenes", "texts": "texts"}


def _library(path):
    if not os.path.isfile(path):
        raise CommandError("NotFound", f"no .blend file at '{path}'")
    if not path.lower().endswith(".blend"):
        raise CommandError("BadRequest", "assets come from .blend files; blender_import_file handles other formats")
    return path


@command("list_assets")
def list_assets(params):
    path = _library(_require(params, "path"))
    wanted = [kind for kind in (params.get("types") or ASSET_TYPES) if kind in ASSET_TYPES]
    with bpy.data.libraries.load(path, link=False, assets_only=bool(params.get("assets_only", False))) as (source, _):
        contents = {kind: sorted(getattr(source, kind)) for kind in wanted}
    return {"path": path, "contents": contents, "count": sum(len(names) for names in contents.values())}


@command("append_assets")
def append_assets(params):
    path = _library(_require(params, "path"))
    kind = (params.get("type") or "objects").lower()
    if kind not in ASSET_TYPES:
        raise CommandError("BadRequest", f"unknown asset type '{kind}'", {"known": sorted(ASSET_TYPES)})
    link = bool(params.get("link", False))
    scene = _scene(params)
    with bpy.data.libraries.load(path, link=link) as (source, target):
        available = list(getattr(source, kind))
        names = list(params.get("names") or available)
        missing = [name for name in names if name not in available]
        if missing:
            raise CommandError("NotFound", f"'{path}' has no {kind} named {', '.join(missing)}", {"available": sorted(available)})
        setattr(target, kind, names)
    loaded = [item for item in getattr(target, kind) if item is not None]
    collection = bpy.data.collections.get(params["collection"]) if params.get("collection") else None
    if params.get("collection") and collection is None:
        collection = bpy.data.collections.new(params["collection"])
        scene.collection.children.link(collection)
    destination = collection or scene.collection
    placed = []
    for item in loaded:
        if kind == "objects" and item.name not in destination.objects:
            destination.objects.link(item)
            placed.append(item.name)
        elif kind == "collections":
            if link:
                instance = bpy.data.objects.new(item.name, None)
                instance.instance_type = "COLLECTION"
                instance.instance_collection = item
                destination.objects.link(instance)
                placed.append(instance.name)
            elif item.name not in destination.children:
                destination.children.link(item)
                placed.append(item.name)
    if kind == "worlds" and loaded and params.get("activate", True):
        scene.world = loaded[0]
    return {"path": path, "type": kind, "link": link, "loaded": [item.name for item in loaded], "placed": placed, "collection": destination.name}
