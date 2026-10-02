"""The compositor node tree by name or spec: nodes, links and sockets."""

import bpy

from .core import _require, command
from .modeling import _apply_settings
from .server import CommandError


def _compositor_tree():
    scene = bpy.context.scene
    if hasattr(scene, "compositing_node_group"):
        tree = scene.compositing_node_group
        if tree is None:
            tree = bpy.data.node_groups.new("Compositing", "CompositorNodeTree")
            scene.compositing_node_group = tree
        if not any(item.item_type == "SOCKET" and item.in_out == "OUTPUT" for item in tree.interface.items_tree):
            tree.interface.new_socket("Image", in_out="OUTPUT", socket_type="NodeSocketColor")
    else:
        scene.use_nodes = True
        tree = scene.node_tree
    if hasattr(scene, "use_nodes"):
        scene.use_nodes = True
    scene.render.use_compositing = True
    if tree is None:
        raise CommandError("Unsupported", "this Blender exposes no compositor node tree")
    return tree


OUTPUT_NODE = "NodeGroupOutput" if "compositing_node_group" in bpy.types.Scene.bl_rna.properties else "CompositorNodeComposite"


COMPOSITOR_ALIASES = {"Composite": OUTPUT_NODE, "Output": OUTPUT_NODE, "MixRGB": "ShaderNodeMix", "Mix": "ShaderNodeMix", "Math": "ShaderNodeMath",
                      "Noise": "ShaderNodeTexNoise", "WhiteNoise": "ShaderNodeTexWhiteNoise"}


def _compositor_type(kind):
    if kind in COMPOSITOR_ALIASES:
        return COMPOSITOR_ALIASES[kind]
    for candidate in (kind, f"CompositorNode{kind}", f"ShaderNode{kind}", f"Node{kind}"):
        if hasattr(bpy.types, candidate):
            return candidate
    raise CommandError("BadRequest", f"unknown compositor node type '{kind}'", {"hint": "use the bl_idname, e.g. CompositorNodeBlur, or its suffix Blur; Mix, Math and Noise come from the shader set"})


def _compositor_node(tree, name):
    node = tree.nodes.get(name)
    if node is None:
        raise CommandError("NotFound", f"the compositor has no node '{name}'", {"nodes": [node.name for node in tree.nodes]})
    return node


def _compositor_socket(node, name, outputs=False):
    sockets = node.outputs if outputs else node.inputs
    socket = sockets.get(name)
    if socket is None and name.isdigit():
        socket = sockets[int(name)]
    if socket is None:
        raise CommandError("UnknownParameter", f"node '{node.name}' has no {'output' if outputs else 'input'} '{name}'", {"sockets": [socket.name for socket in sockets if socket.enabled]})
    return socket


def _set_compositor_socket(socket, value):
    if socket.type == "RGBA" and isinstance(value, list):
        socket.default_value = list(value) + [1.0] * (4 - len(value))
    elif isinstance(value, list):
        socket.default_value = tuple(value)
    else:
        socket.default_value = value


def _render_layers(tree):
    return next((node for node in tree.nodes if node.bl_idname == "CompositorNodeRLayers"), None) or tree.nodes.new("CompositorNodeRLayers")


def _output(tree):
    node = next((node for node in tree.nodes if node.bl_idname == OUTPUT_NODE), None)
    if node is None:
        node = tree.nodes.new(OUTPUT_NODE)
        node.location = (600, 0)
    return node


def _compositor_summary(tree):
    return {
        "nodes": [{"name": node.name, "type": node.bl_idname, "inputs": [socket.name for socket in node.inputs if socket.enabled], "outputs": [socket.name for socket in node.outputs if socket.enabled]} for node in tree.nodes],
        "links": [f"{link.from_node.name}.{link.from_socket.name} -> {link.to_node.name}.{link.to_socket.name}" for link in tree.links],
        "output_node": OUTPUT_NODE,
    }


@command("compositor")
def compositor(params):
    tree = _compositor_tree()
    action = params.get("action") or "info"
    if action == "info":
        return _compositor_summary(tree)
    if action == "clear":
        tree.nodes.clear()
        layers = _render_layers(tree)
        layers.location = (-300, 0)
        tree.links.new(layers.outputs["Image"], _output(tree).inputs[0])
        return _compositor_summary(tree)
    if action == "add_node":
        node = tree.nodes.new(_compositor_type(_require(params, "type")))
        if params.get("name"):
            node.name = params["name"]
        for name, value in (params.get("inputs") or {}).items():
            _set_compositor_socket(_compositor_socket(node, name), value)
        if params.get("properties"):
            _apply_settings(node, params["properties"])
        if params.get("location") is not None:
            node.location = tuple(params["location"])
        return {"node": node.name, "type": node.bl_idname, "inputs": [socket.name for socket in node.inputs if socket.enabled], "outputs": [socket.name for socket in node.outputs if socket.enabled]}
    if action == "link":
        source = _compositor_node(tree, _require(params, "from_node"))
        target = _compositor_node(tree, _require(params, "to_node"))
        tree.links.new(_compositor_socket(source, _require(params, "from_socket"), outputs=True), _compositor_socket(target, _require(params, "to_socket")))
        return {"link": f"{source.name}.{params['from_socket']} -> {target.name}.{params['to_socket']}", "links": len(tree.links)}
    if action == "remove_node":
        node = _compositor_node(tree, _require(params, "node"))
        tree.nodes.remove(node)
        return _compositor_summary(tree)
    if action in ("sharpen", "grain"):
        raise CommandError("BadRequest", f"'{action}' moved to lens_effects, which builds grain weighted by luminance and animated per frame; sharpening is a Filter node via add_node")
    raise CommandError("BadRequest", "action must be info, clear, add_node, link or remove_node")
