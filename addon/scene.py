"""The scene at a glance and the objects in it."""

import bpy

from .core import _name, command, selected_objects
from .objects import _object_summary


@command("scene_info")
def scene_info(params):
    scene = bpy.context.scene
    counts = {}
    for item in scene.objects:
        counts[item.type] = counts.get(item.type, 0) + 1
    return {
        "name": scene.name,
        "file": bpy.data.filepath,
        "mode": bpy.context.mode,
        "frame": {"current": scene.frame_current, "start": scene.frame_start, "end": scene.frame_end, "fps": scene.render.fps},
        "render": {
            "engine": scene.render.engine,
            "resolution": [scene.render.resolution_x, scene.render.resolution_y],
            "percentage": scene.render.resolution_percentage,
        },
        "objects": counts,
        "collections": [collection.name for collection in bpy.data.collections],
        "active": _name(bpy.context.view_layer.objects.active),
        "selected": [item.name for item in selected_objects()],
        "camera": _name(scene.camera),
    }


@command("list_objects")
def list_objects(params):
    wanted_type = params.get("type")
    needle = (params.get("name_contains") or "").lower()
    limit = int(params.get("limit") or 100)
    matched = [
        item for item in bpy.context.scene.objects
        if (not wanted_type or item.type == wanted_type) and needle in item.name.lower()
    ]
    return {
        "total": len(matched),
        "objects": [_object_summary(item) for item in matched[:limit]],
    }
