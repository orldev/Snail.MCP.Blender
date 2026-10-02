"""Modeling commands: bmesh edits on a mesh's selection, modifiers by RNA, curves, joins and origins."""

import math

import bmesh
import bpy
import mathutils

from .core import _name, _object, _require, command
from .objects import _degrees, _ensure_object_mode, _link_new_object, _object_summary, _vector
from .operators import _describe_property, _find_operator, _operator
from .serialize import to_json
from .server import CommandError

AXES = {"x": 0, "y": 1, "z": 2}
CURVE_KINDS = ("bezier", "nurbs", "poly", "circle", "path")


def _mesh_object(name):
    item = _object(name)
    if item.type != "MESH":
        raise CommandError("BadRequest", f"'{item.name}' is a {item.type}, not a mesh")
    return item


def _edit(item, edit):
    _ensure_object_mode()
    mesh = item.data
    bm = bmesh.new()
    bm.from_mesh(mesh)
    bm.verts.ensure_lookup_table()
    bm.edges.ensure_lookup_table()
    bm.faces.ensure_lookup_table()
    try:
        extra = edit(bm) or {}
        bm.to_mesh(mesh)
    finally:
        bm.free()
    mesh.update()
    summary = _mesh_summary(item)
    summary.update(extra)
    return summary


def _mesh_summary(item):
    mesh = item.data
    return {
        "name": item.name,
        "vertices": len(mesh.vertices),
        "edges": len(mesh.edges),
        "faces": len(mesh.polygons),
        "selected": {
            "vertices": sum(1 for vertex in mesh.vertices if vertex.select),
            "edges": sum(1 for edge in mesh.edges if edge.select),
            "faces": sum(1 for face in mesh.polygons if face.select),
        },
    }


def _selected(elements, fallback_all):
    chosen = [element for element in elements if element.select]
    if chosen or not fallback_all:
        return chosen
    return list(elements)


def _axis_vector(spec):
    sign = -1.0 if spec.startswith("-") else 1.0
    axis = spec.lstrip("+-").lower()
    if axis not in AXES:
        raise CommandError("BadRequest", "axis must be one of +x -x +y -y +z -z")
    vector = mathutils.Vector((0.0, 0.0, 0.0))
    vector[AXES[axis]] = sign
    return vector


@command("mesh_select")
def mesh_select(params):
    item = _mesh_object(_require(params, "name"))
    mode = params.get("mode") or "all"

    def select(bm):
        if mode in ("all", "none"):
            for element in list(bm.verts) + list(bm.edges) + list(bm.faces):
                element.select = mode == "all"
        elif mode == "invert":
            for face in bm.faces:
                face.select = not face.select
            bm.select_flush(True)
            bm.select_flush(False)
        elif mode == "by_index":
            _select_by_index(bm, params)
        elif mode == "by_normal":
            _select_by_normal(bm, params)
        elif mode == "by_position":
            _select_by_position(bm, params)
        elif mode == "by_material":
            _select_faces(bm, lambda face: face.material_index == int(params.get("material_index") or 0), params)
        else:
            raise CommandError("BadRequest", "mode must be all, none, invert, by_index, by_normal, by_position or by_material")
        bm.select_flush_mode()

    return _edit(item, select)


def _clear(bm, params):
    if not params.get("extend"):
        for element in list(bm.verts) + list(bm.edges) + list(bm.faces):
            element.select = False


def _select_faces(bm, predicate, params):
    _clear(bm, params)
    for face in bm.faces:
        if predicate(face):
            face.select = True
            for vertex in face.verts:
                vertex.select = True
            for edge in face.edges:
                edge.select = True


def _select_by_index(bm, params):
    _clear(bm, params)
    for kind, table in (("vertices", bm.verts), ("edges", bm.edges), ("faces", bm.faces)):
        for index in params.get(kind) or []:
            if index < 0 or index >= len(table):
                raise CommandError("BadRequest", f"{kind} index {index} is out of range (0..{len(table) - 1})")
            table[index].select = True
    bm.select_flush(True)


def _select_by_normal(bm, params):
    direction = _axis_vector(params.get("axis") or "+z")
    threshold = float(params.get("threshold") if params.get("threshold") is not None else 0.5)
    _select_faces(bm, lambda face: face.normal.dot(direction) >= threshold, params)


def _select_by_position(bm, params):
    axis = (params.get("axis") or "z").lower()
    if axis not in AXES:
        raise CommandError("BadRequest", "axis must be x, y or z")
    low = float(params["min"]) if params.get("min") is not None else float("-inf")
    high = float(params["max"]) if params.get("max") is not None else float("inf")
    index = AXES[axis]
    _clear(bm, params)
    for vertex in bm.verts:
        if low <= vertex.co[index] <= high:
            vertex.select = True
    bm.select_flush(True)
    for face in bm.faces:
        face.select = all(vertex.select for vertex in face.verts)
    for edge in bm.edges:
        edge.select = all(vertex.select for vertex in edge.verts)


@command("mesh_extrude")
def mesh_extrude(params):
    item = _mesh_object(_require(params, "name"))
    distance = float(params.get("distance") if params.get("distance") is not None else 1.0)
    individual = bool(params.get("individual"))

    def extrude(bm):
        faces = _selected(bm.faces, fallback_all=False)
        if not faces:
            raise CommandError("BadRequest", "no faces are selected; call mesh_select first")
        if individual:
            result = bmesh.ops.extrude_discrete_faces(bm, faces=faces)
            for face in result["faces"]:
                bmesh.ops.translate(bm, vec=face.normal * distance, verts=face.verts)
            new_faces = result["faces"]
        else:
            normal = sum((face.normal for face in faces), mathutils.Vector()).normalized()
            result = bmesh.ops.extrude_face_region(bm, geom=faces)
            moved = [element for element in result["geom"] if isinstance(element, bmesh.types.BMVert)]
            bmesh.ops.translate(bm, vec=normal * distance, verts=moved)
            new_faces = [element for element in result["geom"] if isinstance(element, bmesh.types.BMFace)]
            bmesh.ops.delete(bm, geom=faces, context="FACES")
        for element in list(bm.verts) + list(bm.edges) + list(bm.faces):
            element.select = False
        for face in new_faces:
            face.select = True
        bm.select_flush(True)
        return {"extruded_faces": len(new_faces)}

    return _edit(item, extrude)


@command("mesh_inset")
def mesh_inset(params):
    item = _mesh_object(_require(params, "name"))
    thickness = float(params.get("thickness") if params.get("thickness") is not None else 0.1)
    depth = float(params.get("depth") or 0.0)
    individual = bool(params.get("individual"))

    def inset(bm):
        faces = _selected(bm.faces, fallback_all=False)
        if not faces:
            raise CommandError("BadRequest", "no faces are selected; call mesh_select first")
        if individual:
            result = bmesh.ops.inset_individual(bm, faces=faces, thickness=thickness, depth=depth, use_even_offset=True)
        else:
            result = bmesh.ops.inset_region(bm, faces=faces, thickness=thickness, depth=depth, use_even_offset=True)
        return {"inset_faces": len(result["faces"])}

    return _edit(item, inset)


@command("mesh_bevel")
def mesh_bevel(params):
    item = _mesh_object(_require(params, "name"))
    width = float(params.get("width") if params.get("width") is not None else 0.1)
    segments = max(1, int(params.get("segments") or 1))
    vertices_only = bool(params.get("vertices_only"))

    def bevel(bm):
        if vertices_only:
            geometry = _selected(bm.verts, fallback_all=True)
        else:
            edges = _selected(bm.edges, fallback_all=True)
            geometry = edges + list({vertex for edge in edges for vertex in edge.verts})
        result = bmesh.ops.bevel(
            bm, geom=geometry, offset=width, offset_type="OFFSET", segments=segments,
            profile=float(params.get("profile") if params.get("profile") is not None else 0.5),
            affect="VERTICES" if vertices_only else "EDGES", clamp_overlap=True,
        )
        return {"bevel_faces": len(result["faces"])}

    return _edit(item, bevel)


@command("mesh_subdivide")
def mesh_subdivide(params):
    item = _mesh_object(_require(params, "name"))
    cuts = max(1, int(params.get("cuts") or 1))
    smooth = float(params.get("smooth") or 0.0)

    def subdivide(bm):
        edges = _selected(bm.edges, fallback_all=True)
        result = bmesh.ops.subdivide_edges(bm, edges=edges, cuts=cuts, smooth=smooth, use_grid_fill=True)
        return {"split_edges": len([element for element in result["geom_split"] if isinstance(element, bmesh.types.BMEdge)])}

    return _edit(item, subdivide)


@command("mesh_merge")
def mesh_merge(params):
    item = _mesh_object(_require(params, "name"))
    distance = float(params.get("distance") if params.get("distance") is not None else 0.0001)

    def merge(bm):
        before = len(bm.verts)
        bmesh.ops.remove_doubles(bm, verts=_selected(bm.verts, fallback_all=True), dist=distance)
        return {"merged_vertices": before - len(bm.verts)}

    return _edit(item, merge)


@command("mesh_normals")
def mesh_normals(params):
    item = _mesh_object(_require(params, "name"))
    inside = bool(params.get("inside"))

    def normals(bm):
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
        if inside:
            bmesh.ops.reverse_faces(bm, faces=bm.faces)
        return {"recalculated": len(bm.faces), "inside": inside}

    return _edit(item, normals)


@command("mesh_shade")
def mesh_shade(params):
    item = _mesh_object(_require(params, "name"))
    smooth = bool(params.get("smooth", True))
    _ensure_object_mode()
    for polygon in item.data.polygons:
        polygon.use_smooth = smooth
    item.data.update()
    angle = params.get("angle")
    if smooth and angle is not None:
        operator = _find_operator("object.shade_auto_smooth")
        if operator is None:
            raise CommandError("Unsupported", "this Blender has no shade_auto_smooth operator; use mesh_shade without angle")
        with _object_override([item], item):
            operator(angle=math.radians(float(angle)))
    return {"name": item.name, "smooth": smooth, "angle": angle, "modifiers": [modifier.name for modifier in item.modifiers]}


def _object_override(items, active):
    windows = bpy.context.window_manager.windows
    overrides = {"object": active, "active_object": active, "selected_objects": items, "selected_editable_objects": items}
    if windows:
        overrides["window"] = windows[0]
        overrides["screen"] = windows[0].screen
    return bpy.context.temp_override(**overrides)


def _modifier(item, name):
    modifier = item.modifiers.get(name)
    if modifier is None:
        raise CommandError("NotFound", f"'{item.name}' has no modifier named '{name}'", {"modifiers": [modifier.name for modifier in item.modifiers]})
    return modifier


def _modifier_types():
    return [(entry.identifier, entry.name) for entry in bpy.types.Modifier.bl_rna.properties["type"].enum_items]


def _modifier_class(kind):
    wanted = f"{kind.replace('_', '').lower()}modifier"
    for name in dir(bpy.types):
        if name.lower() == wanted:
            return getattr(bpy.types, name)
    return None


def _apply_settings(target, settings):
    known = {prop.identifier: prop for prop in target.bl_rna.properties if not prop.is_readonly}
    unknown = sorted(key for key in settings if key not in known)
    if unknown:
        raise CommandError("UnknownParameter", f"{type(target).__name__} has no setting {', '.join(unknown)}", {"known": sorted(known)})
    for key, value in settings.items():
        setattr(target, key, _coerce_setting(known[key], value))


def _coerce_setting(prop, value):
    if prop.type == "POINTER":
        return _pointer(prop, value)
    if prop.type == "ENUM":
        return set(value) if prop.is_enum_flag and isinstance(value, list) else value
    if getattr(prop, "is_array", False) and isinstance(value, list):
        return tuple(value)
    return value


POINTER_SOURCES = {
    "Object": lambda: bpy.data.objects,
    "Collection": lambda: bpy.data.collections,
    "NodeTree": lambda: bpy.data.node_groups,
    "GeometryNodeTree": lambda: bpy.data.node_groups,
    "Texture": lambda: bpy.data.textures,
    "Image": lambda: bpy.data.images,
    "Material": lambda: bpy.data.materials,
    "Curve": lambda: bpy.data.curves,
}


def _pointer(prop, value):
    if value is None:
        return None
    kind = prop.fixed_type.identifier
    source = POINTER_SOURCES.get(kind)
    if source is None:
        raise CommandError("BadRequest", f"'{prop.identifier}' expects a {kind}, which cannot be named from outside Blender")
    target = source().get(value)
    if target is None:
        raise CommandError("NotFound", f"no {kind} named '{value}' for '{prop.identifier}'")
    return target


def _modifier_summary(modifier):
    settings = {}
    for prop in modifier.bl_rna.properties:
        if prop.identifier in ("rna_type", "name", "type") or prop.is_readonly:
            continue
        value = getattr(modifier, prop.identifier)
        settings[prop.identifier] = _name(value) if prop.type == "POINTER" else to_json(value)
    return {"name": modifier.name, "type": modifier.type, "enabled": modifier.show_viewport, "settings": settings}


@command("add_modifier")
def add_modifier(params):
    item = _object(_require(params, "name"))
    kind = _require(params, "type").upper()
    if kind not in {identifier for identifier, _ in _modifier_types()}:
        raise CommandError("BadRequest", f"unknown modifier type '{kind}'", {"known": [identifier for identifier, _ in _modifier_types()]})
    try:
        modifier = item.modifiers.new(name=params.get("modifier_name") or kind.title().replace("_", " "), type=kind)
    except RuntimeError as error:
        raise CommandError("BadRequest", f"'{item.name}' ({item.type}) cannot take a {kind} modifier: {error}") from error
    _apply_settings(modifier, params.get("settings") or {})
    if params.get("apply"):
        return apply_modifier({"name": item.name, "modifier": modifier.name})
    return _modifier_summary(modifier)


@command("update_modifier")
def update_modifier(params):
    item = _object(_require(params, "name"))
    modifier = _modifier(item, _require(params, "modifier"))
    _apply_settings(modifier, params.get("settings") or {})
    if params.get("new_name"):
        modifier.name = params["new_name"]
    return _modifier_summary(modifier)


@command("remove_modifier")
def remove_modifier(params):
    item = _object(_require(params, "name"))
    modifier = _modifier(item, _require(params, "modifier"))
    removed = modifier.name
    item.modifiers.remove(modifier)
    return {"name": item.name, "removed": removed, "modifiers": [modifier.name for modifier in item.modifiers]}


@command("apply_modifier")
def apply_modifier(params):
    item = _object(_require(params, "name"))
    modifier = _modifier(item, _require(params, "modifier"))
    applied = modifier.name
    _ensure_object_mode()
    with _object_override([item], item):
        bpy.ops.object.modifier_apply(modifier=modifier.name)
    summary = _mesh_summary(item) if item.type == "MESH" else {"name": item.name}
    summary.update({"applied": applied, "modifiers": [modifier.name for modifier in item.modifiers]})
    return summary


@command("describe_modifier")
def describe_modifier(params):
    kind = params.get("type")
    if not kind:
        return {"types": [{"type": identifier, "label": label} for identifier, label in _modifier_types()]}
    cls = _modifier_class(kind.upper())
    if cls is None:
        raise CommandError("BadRequest", f"unknown modifier type '{kind}'", {"known": [identifier for identifier, _ in _modifier_types()]})
    return {
        "type": kind.upper(),
        "label": cls.bl_rna.name,
        "description": cls.bl_rna.description,
        "settings": [
            _describe_property(prop) for prop in cls.bl_rna.properties
            if prop.identifier not in ("rna_type", "name", "type") and not prop.is_readonly
        ],
    }


@command("add_curve")
def add_curve(params):
    kind = params.get("kind") or "bezier"
    if kind not in CURVE_KINDS:
        raise CommandError("BadRequest", f"unknown curve kind '{kind}'", {"known": list(CURVE_KINDS)})
    _ensure_object_mode()
    if kind in ("circle", "path"):
        operator = "curve.primitive_bezier_circle_add" if kind == "circle" else "curve.primitive_nurbs_path_add"
        _operator(operator)(radius=float(params.get("size") or 1.0), location=_vector(params, "location"))
        item = bpy.context.view_layer.objects.active
    else:
        item = _curve_from_points(kind, params)
    if params.get("name"):
        item.name = params["name"]
    return _apply_curve(item, params)


def _curve_from_points(kind, params):
    points = params.get("points") or []
    if len(points) < 2:
        raise CommandError("BadRequest", "'points' needs at least two [x, y, z] entries")
    data = bpy.data.curves.new(params.get("name") or "Curve", "CURVE")
    data.dimensions = "3D"
    spline = data.splines.new({"bezier": "BEZIER", "nurbs": "NURBS", "poly": "POLY"}[kind])
    if kind == "bezier":
        spline.bezier_points.add(len(points) - 1)
        for point, co in zip(spline.bezier_points, points, strict=False):
            point.co = co
            point.handle_left_type = point.handle_right_type = "AUTO"
    else:
        spline.points.add(len(points) - 1)
        for point, co in zip(spline.points, points, strict=False):
            point.co = (*co, 1.0)
        if kind == "nurbs":
            spline.use_endpoint_u = True
    spline.use_cyclic_u = bool(params.get("closed"))
    item = _link_new_object(bpy.data.objects.new(data.name, data))
    item.location = _vector(params, "location")
    return item


@command("set_curve")
def set_curve(params):
    item = _object(_require(params, "name"))
    if item.type != "CURVE":
        raise CommandError("BadRequest", f"'{item.name}' is a {item.type}, not a curve")
    return _apply_curve(item, params)


def _apply_curve(item, params):
    curve = item.data
    for key in ("bevel_depth", "bevel_resolution", "extrude", "resolution_u", "offset"):
        if params.get(key) is not None:
            setattr(curve, key, type(getattr(curve, key))(params[key]))
    if params.get("fill_mode"):
        curve.fill_mode = params["fill_mode"]
    if "bevel_object" in params:
        curve.bevel_mode = "OBJECT" if params["bevel_object"] else "ROUND"
        curve.bevel_object = _object(params["bevel_object"]) if params["bevel_object"] else None
    if "taper_object" in params:
        curve.taper_object = _object(params["taper_object"]) if params["taper_object"] else None
    summary = _object_summary(item)
    summary.update({
        "splines": [{"type": spline.type, "points": len(spline.bezier_points) or len(spline.points), "closed": spline.use_cyclic_u} for spline in curve.splines],
        "bevel_depth": curve.bevel_depth,
        "bevel_resolution": curve.bevel_resolution,
        "extrude": curve.extrude,
        "resolution_u": curve.resolution_u,
        "fill_mode": curve.fill_mode,
        "bevel_object": _name(curve.bevel_object),
        "taper_object": _name(curve.taper_object),
    })
    return summary


@command("convert_to_mesh")
def convert_to_mesh(params):
    item = _object(_require(params, "name"))
    _ensure_object_mode()
    before = set(bpy.data.objects)
    with _object_override([item], item):
        bpy.ops.object.convert(target="MESH", keep_original=bool(params.get("keep_original")))
    created = [candidate for candidate in bpy.data.objects if candidate not in before and candidate.type == "MESH"]
    converted = created[0] if created else item
    if converted.type != "MESH":
        raise CommandError("Unsupported", f"Blender did not convert '{item.name}' ({item.type}) into a mesh")
    return _mesh_summary(converted)


@command("join_objects")
def join_objects(params):
    names = params.get("names") or []
    if len(names) < 2:
        raise CommandError("BadRequest", "'names' needs at least two objects")
    items = [_object(name) for name in names]
    target = _object(params["target"]) if params.get("target") else items[0]
    if target not in items:
        items.append(target)
    _ensure_object_mode()
    with _object_override(items, target):
        bpy.ops.object.join()
    return _mesh_summary(target) if target.type == "MESH" else _object_summary(target)


@command("set_origin")
def set_origin(params):
    item = _object(_require(params, "name"))
    kind = (params.get("type") or "ORIGIN_GEOMETRY").upper()
    center = (params.get("center") or "MEDIAN").upper()
    _ensure_object_mode()
    with _object_override([item], item):
        bpy.ops.object.origin_set(type=kind, center=center)
    return _object_summary(item)


@command("apply_transforms")
def apply_transforms(params):
    item = _object(_require(params, "name"))
    _ensure_object_mode()
    with _object_override([item], item):
        bpy.ops.object.transform_apply(
            location=bool(params.get("location", True)),
            rotation=bool(params.get("rotation", True)),
            scale=bool(params.get("scale", True)),
        )
    summary = _object_summary(item)
    summary["rotation_euler"] = _degrees(item.rotation_euler)
    return summary


GEOMETRY_NODE_PREFIXES = ("GeometryNode", "FunctionNode", "ShaderNode", "Node")


def _geometry_node_type(kind):
    if hasattr(bpy.types, kind):
        return kind
    for prefix in GEOMETRY_NODE_PREFIXES:
        if hasattr(bpy.types, f"{prefix}{kind}"):
            return f"{prefix}{kind}"
    raise CommandError("BadRequest", f"unknown geometry node type '{kind}'", {"hint": "use the bl_idname, e.g. GeometryNodeSubdivideMesh, or its suffix SubdivideMesh"})


def _geometry_tree(item, params):
    modifier_name = params.get("modifier")
    modifier = item.modifiers.get(modifier_name) if modifier_name else next((modifier for modifier in item.modifiers if modifier.type == "NODES"), None)
    if modifier is None:
        modifier = item.modifiers.new(name=modifier_name or "GeometryNodes", type="NODES")
    if modifier.type != "NODES":
        raise CommandError("BadRequest", f"'{modifier.name}' is a {modifier.type} modifier, not Geometry Nodes")
    tree = modifier.node_group
    if tree is None:
        tree = bpy.data.node_groups.new(params.get("tree_name") or f"{item.name} Nodes", "GeometryNodeTree")
        tree.is_modifier = True
        tree.interface.new_socket("Geometry", in_out="INPUT", socket_type="NodeSocketGeometry")
        tree.interface.new_socket("Geometry", in_out="OUTPUT", socket_type="NodeSocketGeometry")
        group_input = tree.nodes.new("NodeGroupInput")
        group_input.name = "Group Input"
        group_input.location = (-400, 0)
        group_output = tree.nodes.new("NodeGroupOutput")
        group_output.name = "Group Output"
        group_output.location = (400, 0)
        tree.links.new(group_input.outputs["Geometry"], group_output.inputs["Geometry"])
        modifier.node_group = tree
    return modifier, tree


def _geometry_socket(node, name, outputs=False):
    sockets = node.outputs if outputs else node.inputs
    socket = sockets.get(name)
    if socket is None and name.isdigit():
        socket = sockets[int(name)]
    if socket is None:
        raise CommandError("UnknownParameter", f"node '{node.name}' has no {'output' if outputs else 'input'} '{name}'", {"sockets": [socket.name for socket in sockets]})
    return socket


def _set_geometry_socket(socket, value):
    if not hasattr(socket, "default_value"):
        raise CommandError("BadRequest", f"'{socket.name}' takes only links")
    current = socket.default_value
    if isinstance(value, list):
        socket.default_value = list(value) + [1.0] * (len(current) - len(value)) if hasattr(current, "__len__") else value
    elif isinstance(current, bool):
        socket.default_value = bool(value)
    elif isinstance(current, int):
        socket.default_value = int(value)
    elif isinstance(current, float):
        socket.default_value = float(value)
    else:
        socket.default_value = value


def _modifier_inputs(modifier):
    """Where a Geometry Nodes modifier keeps the values of its tree inputs: its own interface since Blender 5.0, IDProperties on the modifier itself before that."""
    inputs = getattr(getattr(modifier, "properties", None), "inputs", None)
    return modifier if inputs is None else inputs


def _modifier_input(modifier, identifier):
    """The value a modifier keeps for one tree input; a modifier that has never been given one may not hold the key at all."""
    try:
        return to_json(_modifier_inputs(modifier)[identifier])
    except (KeyError, TypeError):
        return None


def _geometry_tree_summary(modifier, tree):
    return {
        "modifier": modifier.name,
        "tree": tree.name,
        "nodes": [{"name": node.name, "type": node.bl_idname, "inputs": [socket.name for socket in node.inputs], "outputs": [socket.name for socket in node.outputs]} for node in tree.nodes],
        "links": [f"{link.from_node.name}.{link.from_socket.name} -> {link.to_node.name}.{link.to_socket.name}" for link in tree.links],
        "inputs": {socket.name: _modifier_input(modifier, socket.identifier)
                   for socket in tree.interface.items_tree if socket.item_type == "SOCKET" and socket.in_out == "INPUT" and socket.name != "Geometry"},
    }


@command("build_geometry_nodes")
def build_geometry_nodes(params):
    item = _object(_require(params, "name"))
    modifier, tree = _geometry_tree(item, params)
    if params.get("clear"):
        for node in list(tree.nodes):
            if node.bl_idname not in ("NodeGroupInput", "NodeGroupOutput"):
                tree.nodes.remove(node)
        for link in list(tree.links):
            tree.links.remove(link)
    created = {}
    for spec in params.get("nodes") or []:
        node = tree.nodes.new(_geometry_node_type(_require(spec, "type")))
        node.name = spec.get("name") or spec.get("id") or node.name
        for socket_name, value in (spec.get("inputs") or {}).items():
            _set_geometry_socket(_geometry_socket(node, socket_name), value)
        if spec.get("properties"):
            _apply_settings(node, spec["properties"])
        if spec.get("location") is not None:
            node.location = tuple(spec["location"])
        created[spec.get("id") or node.name] = node.name
    for spec in params.get("interface_inputs") or []:
        tree.interface.new_socket(_require(spec, "name"), in_out="INPUT", socket_type=spec.get("socket_type") or "NodeSocketFloat")
    links = []
    for spec in params.get("links") or []:
        if not isinstance(spec, list) or len(spec) != 4:
            raise CommandError("BadRequest", "each link is [from_id, from_socket, to_id, to_socket]")
        source = tree.nodes[created.get(spec[0], spec[0])]
        target = tree.nodes[created.get(spec[2], spec[2])]
        tree.links.new(_geometry_socket(source, spec[1], outputs=True), _geometry_socket(target, spec[3]))
        links.append(f"{source.name}.{spec[1]} -> {target.name}.{spec[3]}")
    if params.get("inputs"):
        set_geometry_inputs({"name": item.name, "modifier": modifier.name, "inputs": params["inputs"]})
    summary = _geometry_tree_summary(modifier, tree)
    summary["created"] = list(created.values())
    summary["new_links"] = links
    return summary


@command("set_geometry_inputs")
def set_geometry_inputs(params):
    item = _object(_require(params, "name"))
    modifier, tree = _geometry_tree(item, params)
    sockets = {socket.name: socket for socket in tree.interface.items_tree if socket.item_type == "SOCKET" and socket.in_out == "INPUT"}
    inputs = _modifier_inputs(modifier)
    for name, value in (params.get("inputs") or {}).items():
        socket = sockets.get(name)
        if socket is None:
            raise CommandError("UnknownParameter", f"the tree has no input '{name}'", {"known": sorted(sockets)})
        if socket.socket_type == "NodeSocketObject":
            inputs[socket.identifier] = _object(value)
        elif isinstance(value, list):
            inputs[socket.identifier] = tuple(value)
        else:
            inputs[socket.identifier] = value
    item.update_tag()
    return _geometry_tree_summary(modifier, tree)


@command("mesh_geometry")
def mesh_geometry(params):
    item = _mesh_object(_require(params, "name"))
    limit = int(params.get("limit") or 500)
    selected_only = bool(params.get("selected_only"))
    world = bool(params.get("world"))
    mesh = item.data
    matrix = item.matrix_world
    vertices = [vertex for vertex in mesh.vertices if not selected_only or vertex.select]
    faces = [face for face in mesh.polygons if not selected_only or face.select]
    return {
        "name": item.name,
        "space": "world" if world else "local",
        "vertices": [[vertex.index, *to_json(matrix @ vertex.co if world else vertex.co)] for vertex in vertices[:limit]],
        "faces": [[face.index, list(face.vertices)] for face in faces[:limit]],
        "counts": {"vertices": len(vertices), "faces": len(faces), "edges": len(mesh.edges)},
        "truncated": len(vertices) > limit or len(faces) > limit,
    }


@command("mesh_set_vertices")
def mesh_set_vertices(params):
    item = _mesh_object(_require(params, "name"))
    mesh = item.data
    relative = bool(params.get("relative"))
    moved = 0
    for entry in params.get("vertices") or []:
        if not isinstance(entry, list) or len(entry) != 4:
            raise CommandError("BadRequest", "each entry is [index, x, y, z]")
        index = int(entry[0])
        if index < 0 or index >= len(mesh.vertices):
            raise CommandError("BadRequest", f"vertex index {index} is out of range (0..{len(mesh.vertices) - 1})")
        vertex = mesh.vertices[index]
        offset = tuple(float(component) for component in entry[1:])
        vertex.co = tuple(a + b for a, b in zip(vertex.co, offset, strict=False)) if relative else offset
        moved += 1
    mesh.update()
    return {"name": item.name, "moved": moved, "relative": relative}
