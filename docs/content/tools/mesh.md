# Mesh editing

Part of the `modeling` skill: these tools appear once it is loaded — by `blender_enable_skill` with `modeling`, or by `find_tool` when a search returns one of them.

## <code>blender_<wbr>mesh_<wbr>select</code>

Selects vertices, edges and faces of a mesh: all, none, invert, by index, by the direction faces point (by_normal with an axis), by coordinate range (by_position), or by material slot. The selection is stored in the mesh and drives every other mesh tool. Returns the counts.

*only adds · every call adds · stays inside Blender.*

| Parameter | Type | Required | Default | Description |
| --- | --- | --- | --- | --- |
| `name` | `string` | yes | — | Name of the mesh object. |
| `mode` | `string` | — | `all` | all, none, invert, by_index, by_normal, by_position or by_material. |
| `vertices` | `array?` | — | `null` | by_index: vertex indices. |
| `edges` | `array?` | — | `null` | by_index: edge indices. |
| `faces` | `array?` | — | `null` | by_index: face indices. |
| `axis` | `string?` | — | `null` | by_normal: facing direction, one of +x -x +y -y +z -z. by_position: x, y or z. |
| `threshold` | `number?` | — | `null` | by_normal: minimum dot product with the axis, 1 is exactly facing; default 0.5. |
| `min` | `number?` | — | `null` | by_position: lowest coordinate along the axis, in the object's local space. |
| `max` | `number?` | — | `null` | by_position: highest coordinate along the axis. |
| `materialIndex` | `integer?` | — | `null` | by_material: material slot index. |
| `extend` | `boolean` | — | `false` | Add to the current selection instead of replacing it. |

<details><summary>JSON Schema</summary>

```json
{
  "type": "object",
  "properties": {
    "name": {
      "description": "Name of the mesh object.",
      "type": "string"
    },
    "mode": {
      "description": "all, none, invert, by_index, by_normal, by_position or by_material.",
      "type": "string",
      "default": "all"
    },
    "vertices": {
      "description": "by_index: vertex indices.",
      "type": [
        "array",
        "null"
      ],
      "items": {
        "type": "integer"
      },
      "default": null
    },
    "edges": {
      "description": "by_index: edge indices.",
      "type": [
        "array",
        "null"
      ],
      "items": {
        "type": "integer"
      },
      "default": null
    },
    "faces": {
      "description": "by_index: face indices.",
      "type": [
        "array",
        "null"
      ],
      "items": {
        "type": "integer"
      },
      "default": null
    },
    "axis": {
      "description": "by_normal: facing direction, one of \u002Bx -x \u002By -y \u002Bz -z. by_position: x, y or z.",
      "type": [
        "string",
        "null"
      ],
      "default": null
    },
    "threshold": {
      "description": "by_normal: minimum dot product with the axis, 1 is exactly facing; default 0.5.",
      "type": [
        "number",
        "null"
      ],
      "default": null
    },
    "min": {
      "description": "by_position: lowest coordinate along the axis, in the object\u0027s local space.",
      "type": [
        "number",
        "null"
      ],
      "default": null
    },
    "max": {
      "description": "by_position: highest coordinate along the axis.",
      "type": [
        "number",
        "null"
      ],
      "default": null
    },
    "materialIndex": {
      "description": "by_material: material slot index.",
      "type": [
        "integer",
        "null"
      ],
      "default": null
    },
    "extend": {
      "description": "Add to the current selection instead of replacing it.",
      "type": "boolean",
      "default": false
    }
  },
  "required": [
    "name"
  ]
}
```

</details>

## <code>blender_<wbr>mesh_<wbr>extrude</code>

Extrudes the selected faces along their normal by a distance; the new faces become the selection, so repeated calls build a tower. Select faces first, e.g. by_normal +z for the top.

*only adds · every call adds · stays inside Blender.*

| Parameter | Type | Required | Default | Description |
| --- | --- | --- | --- | --- |
| `name` | `string` | yes | — | Name of the mesh object. |
| `distance` | `number` | — | `1` | How far to push the selected faces along their normal, in metres; negative pushes inward. |
| `individual` | `boolean` | — | `false` | Extrude each face along its own normal instead of the region as a whole. |

<details><summary>JSON Schema</summary>

```json
{
  "type": "object",
  "properties": {
    "name": {
      "description": "Name of the mesh object.",
      "type": "string"
    },
    "distance": {
      "description": "How far to push the selected faces along their normal, in metres; negative pushes inward.",
      "type": "number",
      "default": 1
    },
    "individual": {
      "description": "Extrude each face along its own normal instead of the region as a whole.",
      "type": "boolean",
      "default": false
    }
  },
  "required": [
    "name"
  ]
}
```

</details>

## <code>blender_<wbr>mesh_<wbr>inset</code>

Insets the selected faces by a thickness, optionally pushing the inset in or out.

*only adds · every call adds · stays inside Blender.*

| Parameter | Type | Required | Default | Description |
| --- | --- | --- | --- | --- |
| `name` | `string` | yes | — | Name of the mesh object. |
| `thickness` | `number` | — | `0.1` | Inset width in metres. |
| `depth` | `number` | — | `0` | Push the inset face along the normal, in metres. |
| `individual` | `boolean` | — | `false` | Inset each face separately instead of the region. |

<details><summary>JSON Schema</summary>

```json
{
  "type": "object",
  "properties": {
    "name": {
      "description": "Name of the mesh object.",
      "type": "string"
    },
    "thickness": {
      "description": "Inset width in metres.",
      "type": "number",
      "default": 0.1
    },
    "depth": {
      "description": "Push the inset face along the normal, in metres.",
      "type": "number",
      "default": 0
    },
    "individual": {
      "description": "Inset each face separately instead of the region.",
      "type": "boolean",
      "default": false
    }
  },
  "required": [
    "name"
  ]
}
```

</details>

## <code>blender_<wbr>mesh_<wbr>bevel</code>

Bevels the selected edges (or vertices) with a width and segment count; with nothing selected, every edge.

*only adds · every call adds · stays inside Blender.*

| Parameter | Type | Required | Default | Description |
| --- | --- | --- | --- | --- |
| `name` | `string` | yes | — | Name of the mesh object. |
| `width` | `number` | — | `0.1` | Bevel width in metres. |
| `segments` | `integer` | — | `1` | Segments per bevelled edge; more is rounder. |
| `profile` | `number` | — | `0.5` | Profile shape 0 to 1; 0.5 is a circular arc. |
| `verticesOnly` | `boolean` | — | `false` | Bevel the selected vertices instead of the selected edges. |

<details><summary>JSON Schema</summary>

```json
{
  "type": "object",
  "properties": {
    "name": {
      "description": "Name of the mesh object.",
      "type": "string"
    },
    "width": {
      "description": "Bevel width in metres.",
      "type": "number",
      "default": 0.1
    },
    "segments": {
      "description": "Segments per bevelled edge; more is rounder.",
      "type": "integer",
      "default": 1
    },
    "profile": {
      "description": "Profile shape 0 to 1; 0.5 is a circular arc.",
      "type": "number",
      "default": 0.5
    },
    "verticesOnly": {
      "description": "Bevel the selected vertices instead of the selected edges.",
      "type": "boolean",
      "default": false
    }
  },
  "required": [
    "name"
  ]
}
```

</details>

## <code>blender_<wbr>mesh_<wbr>subdivide</code>

Subdivides the selected edges (or all) with a number of cuts and optional smoothing.

*only adds · every call adds · stays inside Blender.*

| Parameter | Type | Required | Default | Description |
| --- | --- | --- | --- | --- |
| `name` | `string` | yes | — | Name of the mesh object. |
| `cuts` | `integer` | — | `1` | Cuts per edge. |
| `smooth` | `number` | — | `0` | Smoothness of the new geometry, 0 keeps it flat. |

<details><summary>JSON Schema</summary>

```json
{
  "type": "object",
  "properties": {
    "name": {
      "description": "Name of the mesh object.",
      "type": "string"
    },
    "cuts": {
      "description": "Cuts per edge.",
      "type": "integer",
      "default": 1
    },
    "smooth": {
      "description": "Smoothness of the new geometry, 0 keeps it flat.",
      "type": "number",
      "default": 0
    }
  },
  "required": [
    "name"
  ]
}
```

</details>

## <code>blender_<wbr>mesh_<wbr>merge</code>

Merges vertices closer than a distance (Merge by Distance); the selection or all vertices.

*may overwrite or delete · every call adds · stays inside Blender.*

| Parameter | Type | Required | Default | Description |
| --- | --- | --- | --- | --- |
| `name` | `string` | yes | — | Name of the mesh object. |
| `distance` | `number` | — | `0.0001` | Vertices closer than this, in metres, become one. |

<details><summary>JSON Schema</summary>

```json
{
  "type": "object",
  "properties": {
    "name": {
      "description": "Name of the mesh object.",
      "type": "string"
    },
    "distance": {
      "description": "Vertices closer than this, in metres, become one.",
      "type": "number",
      "default": 0.0001
    }
  },
  "required": [
    "name"
  ]
}
```

</details>

## <code>blender_<wbr>mesh_<wbr>normals</code>

Recalculates face normals to point outside, or inside on request.

*only adds · every call adds · stays inside Blender.*

| Parameter | Type | Required | Default | Description |
| --- | --- | --- | --- | --- |
| `name` | `string` | yes | — | Name of the mesh object. |
| `inside` | `boolean` | — | `false` | Point the normals inward instead of outward. |

<details><summary>JSON Schema</summary>

```json
{
  "type": "object",
  "properties": {
    "name": {
      "description": "Name of the mesh object.",
      "type": "string"
    },
    "inside": {
      "description": "Point the normals inward instead of outward.",
      "type": "boolean",
      "default": false
    }
  },
  "required": [
    "name"
  ]
}
```

</details>

## <code>blender_<wbr>mesh_<wbr>shade</code>

Sets smooth or flat shading on the whole mesh; with an angle, edges sharper than it stay flat.

*only adds · every call adds · stays inside Blender.*

| Parameter | Type | Required | Default | Description |
| --- | --- | --- | --- | --- |
| `name` | `string` | yes | — | Name of the mesh object. |
| `smooth` | `boolean` | — | `true` | Smooth shading; false restores flat shading. |
| `angle` | `number?` | — | `null` | Keep edges sharper than this angle in degrees flat (Blender's Shade Auto Smooth). |

<details><summary>JSON Schema</summary>

```json
{
  "type": "object",
  "properties": {
    "name": {
      "description": "Name of the mesh object.",
      "type": "string"
    },
    "smooth": {
      "description": "Smooth shading; false restores flat shading.",
      "type": "boolean",
      "default": true
    },
    "angle": {
      "description": "Keep edges sharper than this angle in degrees flat (Blender\u0027s Shade Auto Smooth).",
      "type": [
        "number",
        "null"
      ],
      "default": null
    }
  },
  "required": [
    "name"
  ]
}
```

</details>

## <code>blender_<wbr>mesh_<wbr>geometry</code>

Reads the mesh: vertex positions with indices and faces as vertex index lists, in local or world space, limited to a count; the counts cover the whole mesh.

*read-only · every call adds · stays inside Blender.*

| Parameter | Type | Required | Default | Description |
| --- | --- | --- | --- | --- |
| `name` | `string` | yes | — | Name of the mesh object. |
| `limit` | `integer` | — | `500` | At most this many vertices and faces; the counts report the whole mesh. |
| `selectedOnly` | `boolean` | — | `false` | Only the selected vertices and faces. |
| `world` | `boolean` | — | `false` | Coordinates in world space instead of the object's local space. |

<details><summary>JSON Schema</summary>

```json
{
  "type": "object",
  "properties": {
    "name": {
      "description": "Name of the mesh object.",
      "type": "string"
    },
    "limit": {
      "description": "At most this many vertices and faces; the counts report the whole mesh.",
      "type": "integer",
      "default": 500
    },
    "selectedOnly": {
      "description": "Only the selected vertices and faces.",
      "type": "boolean",
      "default": false
    },
    "world": {
      "description": "Coordinates in world space instead of the object\u0027s local space.",
      "type": "boolean",
      "default": false
    }
  },
  "required": [
    "name"
  ]
}
```

</details>

## <code>blender_<wbr>mesh_<wbr>set_<wbr>vertices</code>

Moves vertices by index to new positions or by offsets; the direct way to sculpt exact shapes.

*only adds · every call adds · stays inside Blender.*

| Parameter | Type | Required | Default | Description |
| --- | --- | --- | --- | --- |
| `name` | `string` | yes | — | Name of the mesh object. |
| `vertices` | `array` | yes | — | [[index, x, y, z], ...] in the object's local space. |
| `relative` | `boolean` | — | `false` | Treat x, y, z as offsets from the current position instead of new positions. |

<details><summary>JSON Schema</summary>

```json
{
  "type": "object",
  "properties": {
    "name": {
      "description": "Name of the mesh object.",
      "type": "string"
    },
    "vertices": {
      "description": "[[index, x, y, z], ...] in the object\u0027s local space.",
      "type": "array",
      "items": {
        "type": "array",
        "items": {
          "type": "number"
        }
      }
    },
    "relative": {
      "description": "Treat x, y, z as offsets from the current position instead of new positions.",
      "type": "boolean",
      "default": false
    }
  },
  "required": [
    "name",
    "vertices"
  ]
}
```

</details>

