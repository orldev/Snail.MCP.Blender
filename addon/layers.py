"""View layers with their collections, passes, cryptomatte, AOVs and light groups; light and shadow linking."""

import bpy

from .core import _object, _require, _scene, command
from .rendering import PASSES
from .server import CommandError

CYCLES_PASSES = {"shadow_catcher": "use_pass_shadow_catcher", "volume_direct": "use_pass_volume_direct", "volume_indirect": "use_pass_volume_indirect",
                 "denoising_data": "denoising_store_passes"}
EEVEE_PASSES = {"bloom": "use_pass_bloom", "transparent": "use_pass_transparent"}
COLLECTION_STATES = ("include", "exclude", "holdout", "indirect_only")


def _view_layer(scene, params, create):
    name = params.get("name")
    if not name:
        return bpy.context.view_layer if scene == bpy.context.scene else scene.view_layers[0]
    layer = scene.view_layers.get(name)
    if layer is None and create:
        layer = scene.view_layers.new(name)
    if layer is None:
        raise CommandError("NotFound", f"no view layer named '{name}'", {"known": [layer.name for layer in scene.view_layers]})
    return layer


def _layer_collections(root):
    yield root
    for child in root.children:
        yield from _layer_collections(child)


def _layer_collection_of(layer, name):
    match = next((item for item in _layer_collections(layer.layer_collection) if item.collection.name == name), None)
    if match is None:
        raise CommandError("NotFound", f"the view layer has no collection '{name}'", {"known": [item.collection.name for item in _layer_collections(layer.layer_collection)]})
    return match


def _apply_collections(layer, spec):
    if spec.get("reset"):
        for item in _layer_collections(layer.layer_collection):
            item.exclude = False
            item.holdout = False
            item.indirect_only = False
    for name in spec.get("include") or []:
        item = _layer_collection_of(layer, name)
        item.exclude = False
        item.holdout = False
        item.indirect_only = False
    for name in spec.get("exclude") or []:
        _layer_collection_of(layer, name).exclude = True
    for name in spec.get("holdout") or []:
        _layer_collection_of(layer, name).holdout = True
    for name in spec.get("indirect_only") or []:
        _layer_collection_of(layer, name).indirect_only = True


def _apply_passes(layer, passes):
    for key, enabled in passes.items():
        if key in PASSES and hasattr(layer, PASSES[key]):
            setattr(layer, PASSES[key], bool(enabled))
        elif key in CYCLES_PASSES and hasattr(layer.cycles, CYCLES_PASSES[key]):
            setattr(layer.cycles, CYCLES_PASSES[key], bool(enabled))
        elif key in EEVEE_PASSES and hasattr(layer.eevee, EEVEE_PASSES[key]):
            setattr(layer.eevee, EEVEE_PASSES[key], bool(enabled))
        else:
            raise CommandError("BadRequest", f"unknown render pass '{key}'", {"known": sorted(list(PASSES) + list(CYCLES_PASSES) + list(EEVEE_PASSES))})


def _apply_cryptomatte(layer, spec):
    for key in ("object", "material", "asset"):
        if spec.get(key) is not None:
            setattr(layer, f"use_pass_cryptomatte_{key}", bool(spec[key]))
    if spec.get("levels") is not None:
        layer.pass_cryptomatte_depth = max(2, min(16, int(spec["levels"]) // 2 * 2))
    if spec.get("accurate") is not None:
        layer.use_pass_cryptomatte_accurate = bool(spec["accurate"])


def _apply_aovs(layer, aovs, removals):
    for spec in aovs or []:
        spec = {"name": spec} if isinstance(spec, str) else spec
        name = _require(spec, "name")
        aov = layer.aovs.get(name) or layer.aovs.add()
        aov.name = name
        aov.type = (spec.get("type") or "COLOR").upper()
    for name in removals or []:
        aov = layer.aovs.get(name)
        if aov is not None:
            layer.aovs.remove(aov)


def _apply_light_groups(layer, groups, removals, members):
    for name in groups or []:
        if layer.lightgroups.get(name) is None:
            layer.lightgroups.add(name=name)
    for name in removals or []:
        group = layer.lightgroups.get(name)
        if group is not None:
            layer.lightgroups.remove(group)
    for group, names in (members or {}).items():
        if group and layer.lightgroups.get(group) is None:
            layer.lightgroups.add(name=group)
        for name in names:
            if name == "World" or name == "world":
                bpy.context.scene.world.lightgroup = group
            else:
                _object(name).lightgroup = group


def _collection_tree(item):
    entry = {"name": item.collection.name, "exclude": item.exclude, "holdout": item.holdout, "indirect_only": item.indirect_only}
    if len(item.children) > 0:
        entry["children"] = [_collection_tree(child) for child in item.children]
    return entry


def _layer_summary(scene, layer):
    passes = {name: getattr(layer, attribute) for name, attribute in PASSES.items() if hasattr(layer, attribute)}
    passes.update({name: getattr(layer.cycles, attribute) for name, attribute in CYCLES_PASSES.items() if hasattr(layer.cycles, attribute)})
    return {
        "scene": scene.name,
        "name": layer.name,
        "active": bpy.context.view_layer == layer,
        "use": layer.use,
        "samples": layer.samples,
        "collections": _collection_tree(layer.layer_collection),
        "passes": sorted(name for name, enabled in passes.items() if enabled),
        "cryptomatte": {"object": layer.use_pass_cryptomatte_object, "material": layer.use_pass_cryptomatte_material, "asset": layer.use_pass_cryptomatte_asset,
                        "levels": layer.pass_cryptomatte_depth, "accurate": layer.use_pass_cryptomatte_accurate},
        "aovs": [{"name": aov.name, "type": aov.type} for aov in layer.aovs],
        "light_groups": [group.name for group in layer.lightgroups],
        "material_override": layer.material_override.name if layer.material_override else None,
        "world_override": layer.world_override.name if getattr(layer, "world_override", None) else None,
        "freestyle": layer.use_freestyle,
        "motion_blur": layer.use_motion_blur,
        "layers": [item.name for item in scene.view_layers],
    }


@command("view_layer")
def view_layer(params):
    scene = _scene(params)
    action = params.get("action") or "configure"
    if action == "list":
        return {"scene": scene.name, "active": bpy.context.view_layer.name, "layers": [{"name": layer.name, "use": layer.use} for layer in scene.view_layers]}
    if action == "remove":
        layer = _view_layer(scene, params, False)
        if len(scene.view_layers) == 1:
            raise CommandError("BadRequest", "a scene keeps at least one view layer")
        name = layer.name
        scene.view_layers.remove(layer)
        return {"removed": name, "layers": [item.name for item in scene.view_layers]}
    if action not in ("configure", "create", "activate"):
        raise CommandError("BadRequest", "action must be configure, create, activate, list or remove")
    layer = _view_layer(scene, params, action in ("configure", "create"))
    if action == "activate" or params.get("activate"):
        if scene != bpy.context.scene:
            raise CommandError("BadRequest", "only a view layer of the current scene can be activated")
        bpy.context.window.view_layer = layer
    if params.get("new_name"):
        layer.name = params["new_name"]
    if params.get("use") is not None:
        layer.use = bool(params["use"])
    if params.get("samples") is not None:
        layer.samples = max(0, int(params["samples"]))
    if isinstance(params.get("collections"), dict):
        _apply_collections(layer, params["collections"])
    if isinstance(params.get("passes"), dict):
        _apply_passes(layer, params["passes"])
    if isinstance(params.get("cryptomatte"), dict):
        _apply_cryptomatte(layer, params["cryptomatte"])
    _apply_aovs(layer, params.get("aovs"), params.get("remove_aovs"))
    _apply_light_groups(layer, params.get("light_groups"), params.get("remove_light_groups"), params.get("light_group_members"))
    if "material_override" in params:
        material = params["material_override"]
        layer.material_override = bpy.data.materials.get(material) if material else None
        if material and layer.material_override is None:
            raise CommandError("NotFound", f"no material named '{material}'")
    if "world_override" in params and hasattr(layer, "world_override"):
        world = params["world_override"]
        layer.world_override = bpy.data.worlds.get(world) if world else None
    if params.get("freestyle") is not None:
        layer.use_freestyle = bool(params["freestyle"])
    if params.get("motion_blur") is not None:
        layer.use_motion_blur = bool(params["motion_blur"])
    return _layer_summary(scene, layer)


def _linking_collection(light, attribute, suffix):
    collection = getattr(light.light_linking, attribute)
    if collection is None:
        collection = bpy.data.collections.new(f"{light.name} {suffix}")
        setattr(light.light_linking, attribute, collection)
    return collection


def _link_members(collection, names, state):
    for name in names:
        target = bpy.data.collections.get(name)
        if target is not None and target.name not in collection.children:
            collection.children.link(target)
        elif target is None:
            item = _object(name)
            if item.name not in collection.objects:
                collection.objects.link(item)
    for item, entry in zip(collection.objects, collection.collection_objects, strict=False):
        if item.name in names:
            entry.light_linking.link_state = state
    for child, entry in zip(collection.children, collection.collection_children, strict=False):
        if child.name in names:
            entry.light_linking.link_state = state


def _linking_summary(collection):
    if collection is None:
        return []
    members = [{"name": item.name, "state": entry.light_linking.link_state} for item, entry in zip(collection.objects, collection.collection_objects, strict=False)]
    members += [{"name": child.name, "state": entry.light_linking.link_state, "collection": True} for child, entry in zip(collection.children, collection.collection_children, strict=False)]
    return members


@command("light_linking")
def light_linking(params):
    light = _object(_require(params, "light"))
    if not hasattr(light, "light_linking"):
        raise CommandError("Unsupported", "this Blender has no light linking")
    if params.get("clear"):
        light.light_linking.receiver_collection = None
        light.light_linking.blocker_collection = None
    if params.get("receivers"):
        state = (params.get("receiver_mode") or "include").upper()
        if state not in ("INCLUDE", "EXCLUDE"):
            raise CommandError("BadRequest", "receiver_mode must be include or exclude")
        _link_members(_linking_collection(light, "receiver_collection", "Light Linking"), list(params["receivers"]), state)
    if params.get("blockers"):
        state = (params.get("blocker_mode") or "include").upper()
        if state not in ("INCLUDE", "EXCLUDE"):
            raise CommandError("BadRequest", "blocker_mode must be include or exclude")
        _link_members(_linking_collection(light, "blocker_collection", "Shadow Linking"), list(params["blockers"]), state)
    return {
        "light": light.name,
        "receivers": _linking_summary(light.light_linking.receiver_collection),
        "blockers": _linking_summary(light.light_linking.blocker_collection),
        "engine": bpy.context.scene.render.engine,
    }
