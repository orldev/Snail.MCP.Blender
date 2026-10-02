"""Collections and their view-layer state."""

import bpy

from .core import _object, _require, command
from .server import CommandError


@command("create_collection")
def create_collection(params):
    name = _require(params, "name")
    parent = _collection(params["parent"]) if params.get("parent") else bpy.context.scene.collection
    collection = bpy.data.collections.new(name)
    parent.children.link(collection)
    return _collection_summary(collection)


@command("move_to_collection")
def move_to_collection(params):
    target = _collection(_require(params, "collection"))
    items = [_object(name) for name in params.get("names") or []]
    if not items:
        raise CommandError("BadRequest", "'names' must list at least one object")
    for item in items:
        if not params.get("keep_others"):
            for collection in list(item.users_collection):
                if collection != target:
                    collection.objects.unlink(item)
        if item.name not in target.objects:
            target.objects.link(item)
    return _collection_summary(target)


def _collection(name):
    if name == bpy.context.scene.collection.name:
        return bpy.context.scene.collection
    collection = bpy.data.collections.get(name)
    if collection is None:
        raise CommandError("NotFound", f"no collection named '{name}'", {"collections": [item.name for item in bpy.data.collections]})
    return collection


def _collection_summary(collection):
    return {
        "name": collection.name,
        "objects": [item.name for item in collection.objects][:200],
        "children": [child.name for child in collection.children],
        "hidden": collection.hide_viewport,
    }


def _layer_collection(name, root=None):
    root = root or bpy.context.view_layer.layer_collection
    for child in root.children:
        if child.name == name:
            return child
        found = _layer_collection(name, child)
        if found is not None:
            return found
    return None


@command("update_collection")
def update_collection(params):
    collection = _collection(_require(params, "name"))
    if params.get("hide_viewport") is not None:
        collection.hide_viewport = bool(params["hide_viewport"])
    if params.get("hide_render") is not None:
        collection.hide_render = bool(params["hide_render"])
    layer = _layer_collection(collection.name)
    if params.get("exclude") is not None:
        if layer is None:
            raise CommandError("BadRequest", f"'{collection.name}' is not in the active view layer")
        layer.exclude = bool(params["exclude"])
    if params.get("new_name"):
        collection.name = params["new_name"]
    summary = _collection_summary(collection)
    summary.update({"hide_render": collection.hide_render, "excluded": layer.exclude if layer else None})
    return summary
