# Scene

## <code>blender_<wbr>scene_<wbr>info</code>

The scene at a glance: file path, mode, frame range, render engine and resolution, object counts by type, collections, the active and selected objects, the render camera. Start here before changing anything.

*read-only · every call adds · stays inside Blender.*

Takes no parameters.

<details><summary>JSON Schema</summary>

```json
{
  "type": "object",
  "properties": {}
}
```

</details>

## <code>blender_<wbr>list_<wbr>objects</code>

Objects of the scene with type, location, rotation, scale, visibility and selection; filter by type or name.

*read-only · every call adds · stays inside Blender.*

| Parameter | Type | Required | Default | Description |
| --- | --- | --- | --- | --- |
| `type` | `string?` | — | `null` | Blender object type: MESH, CURVE, SURFACE, META, FONT, ARMATURE, LATTICE, EMPTY, LIGHT, CAMERA, SPEAKER, GREASEPENCIL, VOLUME, POINTCLOUD. |
| `nameContains` | `string?` | — | `null` | Keep only objects whose name contains this text, case-insensitive. |
| `limit` | `integer` | — | `100` | How many objects to return at most; the total is reported regardless. |

<details><summary>JSON Schema</summary>

```json
{
  "type": "object",
  "properties": {
    "type": {
      "description": "Blender object type: MESH, CURVE, SURFACE, META, FONT, ARMATURE, LATTICE, EMPTY, LIGHT, CAMERA, SPEAKER, GREASEPENCIL, VOLUME, POINTCLOUD.",
      "type": [
        "string",
        "null"
      ],
      "default": null
    },
    "nameContains": {
      "description": "Keep only objects whose name contains this text, case-insensitive.",
      "type": [
        "string",
        "null"
      ],
      "default": null
    },
    "limit": {
      "description": "How many objects to return at most; the total is reported regardless.",
      "type": "integer",
      "default": 100
    }
  }
}
```

</details>

## <code>blender_<wbr>object_<wbr>info</code>

One object in depth: transform, dimensions, parent and children, collections, modifiers, materials, visibility and, for meshes, vertex, edge and face counts.

*read-only · every call adds · stays inside Blender.*

| Parameter | Type | Required | Default | Description |
| --- | --- | --- | --- | --- |
| `name` | `string` | yes | — | Name of the object as shown in the outliner. |

<details><summary>JSON Schema</summary>

```json
{
  "type": "object",
  "properties": {
    "name": {
      "description": "Name of the object as shown in the outliner.",
      "type": "string"
    }
  },
  "required": [
    "name"
  ]
}
```

</details>

## <code>blender_<wbr>select_<wbr>objects</code>

Selects objects by name, type or name fragment and makes the last one active. Many operators work on the selection, so call this before blender_run_operator when the target matters.

*only adds · every call adds · stays inside Blender.*

| Parameter | Type | Required | Default | Description |
| --- | --- | --- | --- | --- |
| `names` | `array?` | — | `null` | Object names to select. |
| `type` | `string?` | — | `null` | Blender object type: MESH, CURVE, SURFACE, META, FONT, ARMATURE, LATTICE, EMPTY, LIGHT, CAMERA, SPEAKER, GREASEPENCIL, VOLUME, POINTCLOUD. |
| `nameContains` | `string?` | — | `null` | Select objects whose name contains this text, case-insensitive. |
| `mode` | `string` | — | `replace` | replace (the default: deselect everything first), add (keep the current selection), none (deselect all), all (select all). |

<details><summary>JSON Schema</summary>

```json
{
  "type": "object",
  "properties": {
    "names": {
      "description": "Object names to select.",
      "type": [
        "array",
        "null"
      ],
      "items": {
        "type": [
          "string",
          "null"
        ]
      },
      "default": null
    },
    "type": {
      "description": "Blender object type: MESH, CURVE, SURFACE, META, FONT, ARMATURE, LATTICE, EMPTY, LIGHT, CAMERA, SPEAKER, GREASEPENCIL, VOLUME, POINTCLOUD.",
      "type": [
        "string",
        "null"
      ],
      "default": null
    },
    "nameContains": {
      "description": "Select objects whose name contains this text, case-insensitive.",
      "type": [
        "string",
        "null"
      ],
      "default": null
    },
    "mode": {
      "description": "replace (the default: deselect everything first), add (keep the current selection), none (deselect all), all (select all).",
      "type": "string",
      "default": "replace"
    }
  }
}
```

</details>

## <code>blender_<wbr>scenes</code>

Lists, creates, activates, renames or removes scenes of the file: several shots in one .blend, each with its own camera, frame range and render settings. A new scene is empty or a linked copy sharing the objects.

*may overwrite or delete · every call adds · stays inside Blender.*

| Parameter | Type | Required | Default | Description |
| --- | --- | --- | --- | --- |
| `action` | `string` | — | `list` | list (the default), create, activate, rename, remove. |
| `name` | `string?` | — | `null` | Scene name for create, activate, rename and remove. |
| `copy` | `string?` | — | `null` | create: EMPTY (the default), LINK_COPY (shares the objects) or FULL_COPY (needs Blender's window). |
| `newName` | `string?` | — | `null` | rename: the new name. |
| `activate` | `boolean` | — | `false` | create: make the new scene the active one; needs Blender's window. |
| `scene` | `string?` | — | `null` | create: scene to copy from; the current one otherwise. |

<details><summary>JSON Schema</summary>

```json
{
  "type": "object",
  "properties": {
    "action": {
      "description": "list (the default), create, activate, rename, remove.",
      "type": "string",
      "default": "list"
    },
    "name": {
      "description": "Scene name for create, activate, rename and remove.",
      "type": [
        "string",
        "null"
      ],
      "default": null
    },
    "copy": {
      "description": "create: EMPTY (the default), LINK_COPY (shares the objects) or FULL_COPY (needs Blender\u0027s window).",
      "type": [
        "string",
        "null"
      ],
      "default": null
    },
    "newName": {
      "description": "rename: the new name.",
      "type": [
        "string",
        "null"
      ],
      "default": null
    },
    "activate": {
      "description": "create: make the new scene the active one; needs Blender\u0027s window.",
      "type": "boolean",
      "default": false
    },
    "scene": {
      "description": "create: scene to copy from; the current one otherwise.",
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

