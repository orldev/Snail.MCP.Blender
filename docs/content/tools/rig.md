# Rigging

Part of the `animation` skill: these tools appear once it is loaded — by `blender_enable_skill` with `animation`, or by `find_tool` when a search returns one of them.

## <code>blender_<wbr>add_<wbr>armature</code>

Creates an armature from a list of bones with heads, tails and parents.

*only adds · every call adds · stays inside Blender.*

| Parameter | Type | Required | Default | Description |
| --- | --- | --- | --- | --- |
| `name` | `string?` | — | `null` | Name for the armature object. |
| `bones` | `array?` | — | `null` | Bones as [{"name": "Spine", "head": [0, 0, 0], "tail": [0, 0, 1], "parent": "Root", "connected": true}, ...]; one default bone otherwise. |
| `location` | `array?` | — | `null` | [x, y, z] in metres. |

<details><summary>JSON Schema</summary>

```json
{
  "type": "object",
  "properties": {
    "name": {
      "description": "Name for the armature object.",
      "type": [
        "string",
        "null"
      ],
      "default": null
    },
    "bones": {
      "description": "Bones as [{\u0022name\u0022: \u0022Spine\u0022, \u0022head\u0022: [0, 0, 0], \u0022tail\u0022: [0, 0, 1], \u0022parent\u0022: \u0022Root\u0022, \u0022connected\u0022: true}, ...]; one default bone otherwise.",
      "type": [
        "array",
        "null"
      ],
      "default": null
    },
    "location": {
      "description": "[x, y, z] in metres.",
      "type": [
        "array",
        "null"
      ],
      "items": {
        "type": "number"
      },
      "default": null
    }
  }
}
```

</details>

## <code>blender_<wbr>parent_<wbr>to_<wbr>armature</code>

Skins a mesh to an armature: parents it and creates vertex groups, with automatic weights by default.

*only adds · every call adds · stays inside Blender.*

| Parameter | Type | Required | Default | Description |
| --- | --- | --- | --- | --- |
| `name` | `string` | yes | — | Name of the mesh object. |
| `armature` | `string` | yes | — | Name of the armature object. |
| `method` | `string` | — | `automatic` | automatic (weights from bone distance, the default), envelope, or empty (groups to fill by blender_set_vertex_weights). |

<details><summary>JSON Schema</summary>

```json
{
  "type": "object",
  "properties": {
    "name": {
      "description": "Name of the mesh object.",
      "type": "string"
    },
    "armature": {
      "description": "Name of the armature object.",
      "type": "string"
    },
    "method": {
      "description": "automatic (weights from bone distance, the default), envelope, or empty (groups to fill by blender_set_vertex_weights).",
      "type": "string",
      "default": "automatic"
    }
  },
  "required": [
    "name",
    "armature"
  ]
}
```

</details>

## <code>blender_<wbr>pose_<wbr>bone</code>

Moves, rotates or scales a bone in pose mode and optionally keys it on a frame.

*only adds · every call adds · stays inside Blender.*

| Parameter | Type | Required | Default | Description |
| --- | --- | --- | --- | --- |
| `armature` | `string` | yes | — | Name of the armature object. |
| `bone` | `string` | yes | — | Bone name. |
| `location` | `array?` | — | `null` | [x, y, z] offset in the bone's local space. |
| `rotation` | `array?` | — | `null` | [x, y, z] Euler rotation in degrees. |
| `scale` | `array?` | — | `null` | [x, y, z] scale factors; 1 keeps the size. |
| `frame` | `integer?` | — | `null` | Key the changed channels on this frame. |

<details><summary>JSON Schema</summary>

```json
{
  "type": "object",
  "properties": {
    "armature": {
      "description": "Name of the armature object.",
      "type": "string"
    },
    "bone": {
      "description": "Bone name.",
      "type": "string"
    },
    "location": {
      "description": "[x, y, z] offset in the bone\u0027s local space.",
      "type": [
        "array",
        "null"
      ],
      "items": {
        "type": "number"
      },
      "default": null
    },
    "rotation": {
      "description": "[x, y, z] Euler rotation in degrees.",
      "type": [
        "array",
        "null"
      ],
      "items": {
        "type": "number"
      },
      "default": null
    },
    "scale": {
      "description": "[x, y, z] scale factors; 1 keeps the size.",
      "type": [
        "array",
        "null"
      ],
      "items": {
        "type": "number"
      },
      "default": null
    },
    "frame": {
      "description": "Key the changed channels on this frame.",
      "type": [
        "integer",
        "null"
      ],
      "default": null
    }
  },
  "required": [
    "armature",
    "bone"
  ]
}
```

</details>

## <code>blender_<wbr>set_<wbr>vertex_<wbr>weights</code>

Sets weights of a vertex group by index list, for all vertices or for the selected ones.

*only adds · every call adds · stays inside Blender.*

| Parameter | Type | Required | Default | Description |
| --- | --- | --- | --- | --- |
| `name` | `string` | yes | — | Name of the mesh object. |
| `group` | `string` | yes | — | Vertex group, created when missing; for skinning it is the bone's name. |
| `weights` | `array?` | — | `null` | [[vertexIndex, weight], ...] with weights 0 to 1. |
| `all` | `number?` | — | `null` | Give every vertex this weight. |
| `selected` | `number?` | — | `null` | Give the mesh's selected vertices (blender_mesh_select) this weight. |
| `mode` | `string` | — | `REPLACE` | REPLACE, ADD or SUBTRACT. |

<details><summary>JSON Schema</summary>

```json
{
  "type": "object",
  "properties": {
    "name": {
      "description": "Name of the mesh object.",
      "type": "string"
    },
    "group": {
      "description": "Vertex group, created when missing; for skinning it is the bone\u0027s name.",
      "type": "string"
    },
    "weights": {
      "description": "[[vertexIndex, weight], ...] with weights 0 to 1.",
      "type": [
        "array",
        "null"
      ],
      "items": {
        "type": [
          "array",
          "null"
        ],
        "items": {
          "type": "number"
        }
      },
      "default": null
    },
    "all": {
      "description": "Give every vertex this weight.",
      "type": [
        "number",
        "null"
      ],
      "default": null
    },
    "selected": {
      "description": "Give the mesh\u0027s selected vertices (blender_mesh_select) this weight.",
      "type": [
        "number",
        "null"
      ],
      "default": null
    },
    "mode": {
      "description": "REPLACE, ADD or SUBTRACT.",
      "type": "string",
      "default": "REPLACE"
    }
  },
  "required": [
    "name",
    "group"
  ]
}
```

</details>

