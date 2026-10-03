"""Material commands: Principled BSDF shortcuts, node graphs by name, textures, UVs and viewport shading."""

import math
import os

import bpy

from .core import _name, _object, _require, command
from .modeling import _apply_settings
from .serialize import to_json
from .server import CommandError

PRINCIPLED_INPUTS = {
    "base_color": ("Base Color", None),
    "metallic": ("Metallic", None),
    "roughness": ("Roughness", None),
    "ior": ("IOR", None),
    "alpha": ("Alpha", None),
    "emission_color": ("Emission Color", "Emission"),
    "emission_strength": ("Emission Strength", None),
    "transmission": ("Transmission Weight", "Transmission"),
    "subsurface": ("Subsurface Weight", "Subsurface"),
    "coat": ("Coat Weight", "Clearcoat"),
    "sheen": ("Sheen Weight", "Sheen"),
    "specular": ("Specular IOR Level", "Specular"),
}

PROCEDURAL_TEXTURES = {
    "noise": "ShaderNodeTexNoise",
    "voronoi": "ShaderNodeTexVoronoi",
    "musgrave": "ShaderNodeTexNoise",
    "magic": "ShaderNodeTexMagic",
    "wave": "ShaderNodeTexWave",
    "brick": "ShaderNodeTexBrick",
    "checker": "ShaderNodeTexChecker",
    "gradient": "ShaderNodeTexGradient",
}

UV_METHODS = ("smart", "unwrap", "cube", "sphere", "cylinder")
SHADING_MODES = ("WIREFRAME", "SOLID", "MATERIAL", "RENDERED")


def _existing(name):
    material = bpy.data.materials.get(name)
    if material is None:
        raise CommandError("NotFound", f"no material named '{name}'", {"materials": [item.name for item in bpy.data.materials][:100]})
    return material


def _material(name):
    """A material about to be edited: its nodes switched on, since every edit here goes through them."""
    material = _existing(name)
    if not material.use_nodes:
        material.use_nodes = True
    return material


def _found_principled(material):
    tree = material.node_tree
    return next((node for node in tree.nodes if node.type == "BSDF_PRINCIPLED"), None) if tree is not None else None


def _principled(material):
    """The Principled BSDF an edit writes to, created and wired to the output when the material has none; only edits call this."""
    tree = material.node_tree
    node = _found_principled(material)
    if node is None:
        node = tree.nodes.new("ShaderNodeBsdfPrincipled")
        output = next((node for node in tree.nodes if node.type == "OUTPUT_MATERIAL"), None) or tree.nodes.new("ShaderNodeOutputMaterial")
        tree.links.new(node.outputs["BSDF"], output.inputs["Surface"])
    return node


def _find_socket(sockets, key):
    """A socket by its name, its identifier or its position, in that order.

    A name is not always enough: the three inputs of a Math node are all called Value, so the second operand could
    not be reached at all. Its identifier (Value_001) or its index (1) reaches it; a name still wins when it is unique.
    """
    key = str(key)
    socket = sockets.get(key)
    if socket is None:
        socket = next((candidate for candidate in sockets if candidate.identifier == key), None)
    if socket is None and key.isdigit() and int(key) < len(sockets):
        socket = sockets[int(key)]
    return socket


def _socket_key(socket, sockets):
    """The name a socket is reported under: its own, or its identifier when another socket shares the name — keyed by
    name alone, the three Value inputs of a Math node reported as one."""
    return socket.identifier if sum(other.name == socket.name for other in sockets) > 1 else socket.name


def _socket_names(sockets):
    """How to name each socket, with the identifier spelled out wherever a name is shared."""
    shared = {socket.name for socket in sockets if sum(other.name == socket.name for other in sockets) > 1}
    return [f"{socket.name} ({socket.identifier})" if socket.name in shared else socket.name for socket in sockets]


def _socket(node, name, fallback=None):
    socket = _find_socket(node.inputs, name) or (_find_socket(node.inputs, fallback) if fallback else None)
    if socket is None:
        raise CommandError("UnknownParameter", f"node '{node.name}' has no input '{name}'; name one by its name, its identifier or its index",
                           {"inputs": _socket_names(node.inputs)})
    return socket


def _set_socket(socket, value):
    if socket.type == "SHADER":
        raise CommandError("BadRequest", f"'{socket.name}' is a shader socket; link a node into it instead")
    if socket.type == "RGBA":
        color = list(value)
        if len(color) == 3:
            color.append(1.0)
        socket.default_value = color
    elif socket.type == "VECTOR":
        socket.default_value = tuple(value)
    elif socket.type == "BOOLEAN":
        socket.default_value = bool(value)
    elif socket.type == "INT":
        socket.default_value = int(value)
    elif socket.type == "STRING":
        socket.default_value = str(value)
    else:
        socket.default_value = float(value)


def _socket_value(socket):
    if socket.is_linked:
        return {"linked_from": [f"{link.from_node.name}.{link.from_socket.name}" for link in socket.links]}
    if not hasattr(socket, "default_value"):
        return None
    return to_json(socket.default_value)


def _apply_principled(material, params):
    node = _principled(material)
    for key, (name, fallback) in PRINCIPLED_INPUTS.items():
        if params.get(key) is not None:
            _set_socket(_socket(node, name, fallback), params[key])
    if params.get("blend_method"):
        material.blend_method = params["blend_method"]
    return _material_summary(material)


def _material_summary(material):
    """What a material is, read without touching it: a material with no Principled BSDF reports none rather than being given one."""
    node = _found_principled(material)
    return {
        "name": material.name,
        "users": material.users,
        "nodes": len(material.node_tree.nodes) if material.node_tree is not None else 0,
        "blend_method": material.blend_method,
        "principled": None if node is None else {
            key: _socket_value(node.inputs[name] if name in node.inputs else node.inputs[fallback])
            for key, (name, fallback) in PRINCIPLED_INPUTS.items()
            if name in node.inputs or (fallback and fallback in node.inputs)},
    }


@command("create_material")
def create_material(params):
    material = bpy.data.materials.new(params.get("name") or "Material")
    material.use_nodes = True
    _apply_principled(material, params)
    assigned = assign_material({"object": params["assign_to"], "material": material.name}) if params.get("assign_to") else None
    summary = _material_summary(material)
    if assigned:
        summary["assigned"] = assigned
    return summary


@command("set_material")
def set_material(params):
    material = _material(_require(params, "name"))
    if params.get("new_name"):
        material.name = params["new_name"]
    return _apply_principled(material, params)


@command("assign_material")
def assign_material(params):
    """Gives an object a material: by default the object becomes that material, and its faces follow it into the slot.

    It used to add a slot at the end and leave every face pointing at slot 0, so the reply said ok, the slot list grew by one
    and the object rendered exactly as before. Adding a slot is what a second material on part of a mesh needs, so it is still
    here — asked for by 'append' or by 'selected_faces', which is the call that has faces of its own to point somewhere.
    """
    item = _object(_require(params, "object"))
    material = _material(_require(params, "material"))
    if not hasattr(item.data, "materials"):
        raise CommandError("BadRequest", f"'{item.name}' ({item.type}) takes no materials")
    chosen_faces = bool(params.get("selected_faces")) and item.type == "MESH"
    adding = bool(params.get("append")) or chosen_faces
    slot = params.get("slot")
    if slot is not None:
        slot = int(slot)
        while len(item.data.materials) <= slot:
            item.data.materials.append(None)
        item.data.materials[slot] = material
    elif adding:
        if material.name in item.data.materials:
            slot = list(item.data.materials).index(material)
        else:
            item.data.materials.append(material)
            slot = len(item.data.materials) - 1
    else:
        slot = 0
        if len(item.data.materials):
            item.data.materials[0] = material
        else:
            item.data.materials.append(material)
    faces = []
    if item.type == "MESH":
        if chosen_faces:
            faces = [polygon for polygon in item.data.polygons if polygon.select]
        elif not adding:
            faces = list(item.data.polygons)
    for polygon in faces:
        polygon.material_index = slot
    return {"object": item.name, "material": material.name, "slot": slot, "faces_pointed_at_it": len(faces),
            "slots": [_name(entry) for entry in item.data.materials]}


@command("list_materials")
def list_materials(params):
    return {"materials": [{"name": material.name, "users": material.users, "nodes": material.use_nodes} for material in bpy.data.materials]}


@command("material_info")
def material_info(params):
    material = _existing(_require(params, "name"))
    tree = material.node_tree
    summary = _material_summary(material)
    summary["graph"] = {
        "nodes": [_node_summary(node) for node in tree.nodes] if tree is not None else [],
        "links": [f"{link.from_node.name}.{link.from_socket.name} -> {link.to_node.name}.{link.to_socket.name}" for link in tree.links] if tree is not None else [],
    }
    return summary


def _node_summary(node):
    return {
        "name": node.name,
        "type": node.bl_idname,
        "label": node.label,
        "location": [round(node.location.x), round(node.location.y)],
        "inputs": {_socket_key(socket, node.inputs): _socket_value(socket) for socket in node.inputs if socket.enabled},
        "outputs": [_socket_key(socket, node.outputs) for socket in node.outputs if socket.enabled],
    }


def _node_type(kind):
    idname = kind if kind.startswith(("ShaderNode", "NodeGroup", "NodeReroute", "NodeFrame")) else f"ShaderNode{kind}"
    if not hasattr(bpy.types, idname):
        raise CommandError("BadRequest", f"unknown node type '{kind}'", {"hint": "use the bl_idname, e.g. ShaderNodeTexNoise, or its suffix TexNoise"})
    return idname


def _node(material, name):
    node = material.node_tree.nodes.get(name)
    if node is None:
        raise CommandError("NotFound", f"material '{material.name}' has no node '{name}'", {"nodes": [node.name for node in material.node_tree.nodes]})
    return node


def _configure_node(node, params):
    for name, value in (params.get("inputs") or {}).items():
        _set_socket(_socket(node, name), value)
    if params.get("properties"):
        _apply_settings(node, params["properties"])
    if params.get("label"):
        node.label = params["label"]
    if params.get("location") is not None:
        node.location = tuple(params["location"])


@command("add_node")
def add_node(params):
    material = _material(_require(params, "material"))
    node = material.node_tree.nodes.new(_node_type(_require(params, "type")))
    if params.get("name"):
        node.name = params["name"]
    _configure_node(node, params)
    return _node_summary(node)


@command("set_node")
def set_node(params):
    material = _material(_require(params, "material"))
    node = _node(material, _require(params, "node"))
    _configure_node(node, params)
    if params.get("new_name"):
        node.name = params["new_name"]
    return _node_summary(node)


@command("remove_node")
def remove_node(params):
    material = _material(_require(params, "material"))
    node = _node(material, _require(params, "node"))
    removed = node.name
    material.node_tree.nodes.remove(node)
    return {"material": material.name, "removed": removed, "nodes": [node.name for node in material.node_tree.nodes]}


@command("link_nodes")
def link_nodes(params):
    material = _material(_require(params, "material"))
    link = _link(material, _require(params, "from_node"), _require(params, "from_socket"), _require(params, "to_node"), _require(params, "to_socket"))
    return {"material": material.name, "link": link, "links": len(material.node_tree.links)}


def _link(material, from_name, from_socket, to_name, to_socket):
    source = _node(material, from_name)
    target = _node(material, to_name)
    output = _find_socket(source.outputs, from_socket)
    if output is None:
        raise CommandError("UnknownParameter", f"node '{source.name}' has no output '{from_socket}'; name one by its name, its identifier or its index",
                           {"outputs": _socket_names(source.outputs)})
    socket = _socket(target, to_socket)
    material.node_tree.links.new(output, socket)
    return f"{source.name}.{output.name} -> {target.name}.{socket.name}"


@command("build_node_graph")
def build_node_graph(params):
    material = _material(_require(params, "material"))
    tree = material.node_tree
    if params.get("clear"):
        tree.nodes.clear()
    created = {}
    for spec in params.get("nodes") or []:
        node = tree.nodes.new(_node_type(_require(spec, "type")))
        node.name = spec.get("name") or spec.get("id") or node.name
        _configure_node(node, spec)
        created[spec.get("id") or node.name] = node.name
    links = []
    for spec in params.get("links") or []:
        if not isinstance(spec, list) or len(spec) != 4:
            raise CommandError("BadRequest", "each link is [from_id, from_socket, to_id, to_socket]")
        links.append(_link(material, created.get(spec[0], spec[0]), spec[1], created.get(spec[2], spec[2]), spec[3]))
    return {"material": material.name, "nodes": list(created.values()), "links": links, "total_nodes": len(tree.nodes)}


@command("load_image_texture")
def load_image_texture(params):
    material = _material(_require(params, "material"))
    path = _require(params, "path")
    if not os.path.isfile(path):
        raise CommandError("NotFound", f"no image file at '{path}'")
    image = bpy.data.images.load(path, check_existing=True)
    if params.get("colorspace"):
        image.colorspace_settings.name = params["colorspace"]
    node = material.node_tree.nodes.new("ShaderNodeTexImage")
    node.image = image
    node.name = params.get("name") or f"Image {image.name}"
    node.location = (-600, 300)
    if params.get("projection"):
        node.projection = params["projection"]
    connect_to = params.get("connect_to", "Base Color")
    link = None
    if connect_to:
        socket_name = "Alpha" if connect_to == "Alpha" else "Color"
        link = _link(material, node.name, socket_name, _principled(material).name, connect_to)
    return {"material": material.name, "node": _node_summary(node), "image": {"name": image.name, "size": list(image.size)}, "link": link}


@command("procedural_texture")
def procedural_texture(params):
    material = _material(_require(params, "material"))
    kind = params.get("kind") or "noise"
    if kind not in PROCEDURAL_TEXTURES:
        raise CommandError("BadRequest", f"unknown procedural texture '{kind}'", {"known": sorted(PROCEDURAL_TEXTURES)})
    tree = material.node_tree
    texture = tree.nodes.new(PROCEDURAL_TEXTURES[kind])
    texture.name = params.get("name") or f"{kind.title()} Texture"
    texture.location = (-800, 0)
    for name, value in (params.get("inputs") or {}).items():
        _set_socket(_socket(texture, name), value)
    if params.get("properties"):
        _apply_settings(texture, params["properties"])
    output = "Fac" if "Fac" in texture.outputs else ("Color" if "Color" in texture.outputs else texture.outputs[0].name)
    last_node, last_socket = texture, output
    if params.get("color_ramp"):
        ramp = tree.nodes.new("ShaderNodeValToRGB")
        ramp.name = f"{texture.name} Ramp"
        ramp.location = (-500, 0)
        stops = params["color_ramp"] if isinstance(params["color_ramp"], list) else None
        if stops:
            _apply_ramp(ramp, stops)
        _link(material, texture.name, "Fac" if "Fac" in texture.outputs else output, ramp.name, "Fac")
        last_node, last_socket = ramp, "Color"
    connect_to = params.get("connect_to", "Base Color")
    link = None
    if connect_to:
        if connect_to == "Normal":
            bump = tree.nodes.new("ShaderNodeBump")
            bump.name = f"{texture.name} Bump"
            bump.location = (-250, -200)
            if params.get("bump_strength") is not None:
                bump.inputs["Strength"].default_value = float(params["bump_strength"])
            _link(material, last_node.name, last_socket, bump.name, "Height")
            last_node, last_socket = bump, "Normal"
        link = _link(material, last_node.name, last_socket, _principled(material).name, connect_to)
    return {"material": material.name, "texture": _node_summary(texture), "link": link}


def _apply_ramp(ramp, stops):
    elements = ramp.color_ramp.elements
    while len(elements) > 1:
        elements.remove(elements[-1])
    for index, stop in enumerate(stops):
        element = elements[0] if index == 0 else elements.new(float(stop["position"]))
        element.position = float(stop["position"])
        color = list(stop["color"])
        element.color = [*color, 1.0] if len(color) == 3 else color


@command("unwrap_uv")
def unwrap_uv(params):
    item = _object(_require(params, "name"))
    if item.type != "MESH":
        raise CommandError("BadRequest", f"'{item.name}' is a {item.type}, not a mesh")
    method = params.get("method") or "smart"
    if method not in UV_METHODS:
        raise CommandError("BadRequest", f"unknown method '{method}'", {"known": list(UV_METHODS)})
    margin = float(params.get("margin") if params.get("margin") is not None else 0.02)
    with _uv_override(item):
        bpy.ops.object.mode_set(mode="EDIT")
        try:
            bpy.ops.mesh.select_all(action="SELECT")
            if method == "smart":
                bpy.ops.uv.smart_project(angle_limit=math.radians(float(params.get("angle_limit") or 66.0)), island_margin=margin)
            elif method == "unwrap":
                bpy.ops.uv.unwrap(margin=margin)
            elif method == "cube":
                bpy.ops.uv.cube_project(cube_size=float(params.get("size") or 1.0))
            elif method == "sphere":
                bpy.ops.uv.sphere_project()
            else:
                bpy.ops.uv.cylinder_project()
        finally:
            bpy.ops.object.mode_set(mode="OBJECT")
    return {"name": item.name, "method": method, "uv_maps": [layer.name for layer in item.data.uv_layers], "active_uv": _name(item.data.uv_layers.active)}


def _uv_override(item):
    overrides = {"object": item, "active_object": item, "selected_objects": [item], "selected_editable_objects": [item], "edit_object": item}
    for window in bpy.context.window_manager.windows:
        for area in window.screen.areas:
            if area.type == "VIEW_3D":
                region = next((region for region in area.regions if region.type == "WINDOW"), None)
                overrides.update({"window": window, "screen": window.screen, "area": area, "region": region})
                return bpy.context.temp_override(**overrides)
    return bpy.context.temp_override(**overrides)


@command("set_viewport_shading")
def set_viewport_shading(params):
    mode = (params.get("mode") or "MATERIAL").upper()
    if mode not in SHADING_MODES:
        raise CommandError("BadRequest", f"unknown shading mode '{mode}'", {"known": list(SHADING_MODES)})
    changed = 0
    for window in bpy.context.window_manager.windows:
        for area in window.screen.areas:
            if area.type == "VIEW_3D":
                area.spaces.active.shading.type = mode
                changed += 1
    if changed == 0:
        raise CommandError("NoArea", "no 3D viewport is open in Blender")
    return {"mode": mode, "viewports": changed}
