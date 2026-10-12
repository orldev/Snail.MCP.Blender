# Modifiers

Part of the `modeling` skill: these tools appear once it is loaded — by `blender_enable_skill` with `modeling`, or by `find_tool` when a search returns one of them.

## <code>blender_<wbr>add_<wbr>modifier</code>

Adds a modifier of any type to an object with its settings, optionally applying it at once. Returns the modifier with the effective values of every setting.

*only adds · every call adds · stays inside Blender.*

| Parameter | Type | Required | Default | Description |
| --- | --- | --- | --- | --- |
| `name` | `string` | yes | — | Name of the object as shown in the outliner. |
| `type` | `string` | yes | — | Modifier type as Blender names it: ARRAY, BEVEL, BOOLEAN, BUILD, DECIMATE, EDGE_SPLIT, MIRROR, REMESH, SCREW, SKIN, SOLIDIFY, SUBSURF, TRIANGULATE, WELD, WIREFRAME, CAST, CURVE, DISPLACE, LATTICE, MESH_DEFORM, SHRINKWRAP, SIMPLE_DEFORM, SMOOTH, WARP, WAVE, NODES and the rest; blender_describe_modifier lists them all. |
| `settings` | `object?` | — | `null` | Settings as a JSON object keyed by the modifier's Python property names, e.g. ARRAY {"count": 3}, BOOLEAN {"operation": "DIFFERENCE", "object": "Cutter"}, MIRROR {"use_axis": [true, false, false], "use_clip": true}, SUBSURF {"levels": 2}, SOLIDIFY {"thickness": 0.05}. Objects, collections and node groups are named by their name. Unknown keys are rejected with the list of known ones; blender_describe_modifier documents them. |
| `modifierName` | `string?` | — | `null` | Name for the modifier; the type's label otherwise. |
| `apply` | `boolean` | — | `false` | Apply it to the geometry right away and drop it from the stack. |

<details><summary>JSON Schema</summary>

```json
{
  "type": "object",
  "properties": {
    "name": {
      "description": "Name of the object as shown in the outliner.",
      "type": "string"
    },
    "type": {
      "description": "Modifier type as Blender names it: ARRAY, BEVEL, BOOLEAN, BUILD, DECIMATE, EDGE_SPLIT, MIRROR, REMESH, SCREW, SKIN, SOLIDIFY, SUBSURF, TRIANGULATE, WELD, WIREFRAME, CAST, CURVE, DISPLACE, LATTICE, MESH_DEFORM, SHRINKWRAP, SIMPLE_DEFORM, SMOOTH, WARP, WAVE, NODES and the rest; blender_describe_modifier lists them all.",
      "type": "string"
    },
    "settings": {
      "description": "Settings as a JSON object keyed by the modifier\u0027s Python property names, e.g. ARRAY {\u0022count\u0022: 3}, BOOLEAN {\u0022operation\u0022: \u0022DIFFERENCE\u0022, \u0022object\u0022: \u0022Cutter\u0022}, MIRROR {\u0022use_axis\u0022: [true, false, false], \u0022use_clip\u0022: true}, SUBSURF {\u0022levels\u0022: 2}, SOLIDIFY {\u0022thickness\u0022: 0.05}. Objects, collections and node groups are named by their name. Unknown keys are rejected with the list of known ones; blender_describe_modifier documents them.",
      "type": [
        "object",
        "null"
      ],
      "default": null
    },
    "modifierName": {
      "description": "Name for the modifier; the type\u0027s label otherwise.",
      "type": [
        "string",
        "null"
      ],
      "default": null
    },
    "apply": {
      "description": "Apply it to the geometry right away and drop it from the stack.",
      "type": "boolean",
      "default": false
    }
  },
  "required": [
    "name",
    "type"
  ]
}
```

</details>

## <code>blender_<wbr>update_<wbr>modifier</code>

Changes settings of an existing modifier, or renames it.

*only adds · every call adds · stays inside Blender.*

| Parameter | Type | Required | Default | Description |
| --- | --- | --- | --- | --- |
| `name` | `string` | yes | — | Name of the object as shown in the outliner. |
| `modifier` | `string` | yes | — | Modifier name as shown in the stack; blender_object_info lists them. |
| `settings` | `object?` | — | `null` | Settings as a JSON object keyed by the modifier's Python property names, e.g. ARRAY {"count": 3}, BOOLEAN {"operation": "DIFFERENCE", "object": "Cutter"}, MIRROR {"use_axis": [true, false, false], "use_clip": true}, SUBSURF {"levels": 2}, SOLIDIFY {"thickness": 0.05}. Objects, collections and node groups are named by their name. Unknown keys are rejected with the list of known ones; blender_describe_modifier documents them. |
| `newName` | `string?` | — | `null` | Rename the modifier. |

<details><summary>JSON Schema</summary>

```json
{
  "type": "object",
  "properties": {
    "name": {
      "description": "Name of the object as shown in the outliner.",
      "type": "string"
    },
    "modifier": {
      "description": "Modifier name as shown in the stack; blender_object_info lists them.",
      "type": "string"
    },
    "settings": {
      "description": "Settings as a JSON object keyed by the modifier\u0027s Python property names, e.g. ARRAY {\u0022count\u0022: 3}, BOOLEAN {\u0022operation\u0022: \u0022DIFFERENCE\u0022, \u0022object\u0022: \u0022Cutter\u0022}, MIRROR {\u0022use_axis\u0022: [true, false, false], \u0022use_clip\u0022: true}, SUBSURF {\u0022levels\u0022: 2}, SOLIDIFY {\u0022thickness\u0022: 0.05}. Objects, collections and node groups are named by their name. Unknown keys are rejected with the list of known ones; blender_describe_modifier documents them.",
      "type": [
        "object",
        "null"
      ],
      "default": null
    },
    "newName": {
      "description": "Rename the modifier.",
      "type": [
        "string",
        "null"
      ],
      "default": null
    }
  },
  "required": [
    "name",
    "modifier"
  ]
}
```

</details>

## <code>blender_<wbr>remove_<wbr>modifier</code>

Removes a modifier from the stack without applying it.

*may overwrite or delete · every call adds · stays inside Blender.*

| Parameter | Type | Required | Default | Description |
| --- | --- | --- | --- | --- |
| `name` | `string` | yes | — | Name of the object as shown in the outliner. |
| `modifier` | `string` | yes | — | Modifier name as shown in the stack; blender_object_info lists them. |

<details><summary>JSON Schema</summary>

```json
{
  "type": "object",
  "properties": {
    "name": {
      "description": "Name of the object as shown in the outliner.",
      "type": "string"
    },
    "modifier": {
      "description": "Modifier name as shown in the stack; blender_object_info lists them.",
      "type": "string"
    }
  },
  "required": [
    "name",
    "modifier"
  ]
}
```

</details>

## <code>blender_<wbr>apply_<wbr>modifier</code>

Bakes a modifier into the mesh and removes it from the stack; irreversible except through undo.

*may overwrite or delete · every call adds · stays inside Blender.*

| Parameter | Type | Required | Default | Description |
| --- | --- | --- | --- | --- |
| `name` | `string` | yes | — | Name of the object as shown in the outliner. |
| `modifier` | `string` | yes | — | Modifier name as shown in the stack; blender_object_info lists them. |

<details><summary>JSON Schema</summary>

```json
{
  "type": "object",
  "properties": {
    "name": {
      "description": "Name of the object as shown in the outliner.",
      "type": "string"
    },
    "modifier": {
      "description": "Modifier name as shown in the stack; blender_object_info lists them.",
      "type": "string"
    }
  },
  "required": [
    "name",
    "modifier"
  ]
}
```

</details>

## <code>blender_<wbr>describe_<wbr>modifier</code>

Lists every modifier type, or documents one type's settings with types, defaults, limits and enum values for blender_add_modifier and blender_update_modifier.

*read-only · every call adds · stays inside Blender.*

| Parameter | Type | Required | Default | Description |
| --- | --- | --- | --- | --- |
| `type` | `string?` | — | `null` | Modifier type to document; empty lists every type Blender has. |

<details><summary>JSON Schema</summary>

```json
{
  "type": "object",
  "properties": {
    "type": {
      "description": "Modifier type to document; empty lists every type Blender has.",
      "type": [
        "string",
        "null"
      ],
      "default": null
    }
  }
}
```

</details>

