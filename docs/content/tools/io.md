# Import, export and assets

Part of the `io` skill: these tools appear once it is loaded — by `blender_enable_skill` with `io`, or by `find_tool` when a search returns one of them.

## <code>blender_<wbr>import_<wbr>file</code>

Imports a model or scene file into the current scene and lists the objects it added; the format follows the extension.

*only adds · every call adds · stays inside Blender.*

| Parameter | Type | Required | Default | Description |
| --- | --- | --- | --- | --- |
| `path` | `string` | yes | — | Absolute path of the file to import. |
| `format` | `string?` | — | `null` | Format when the extension does not say: fbx, obj, stl, ply, gltf, glb, dae, usd, usda, usdc, usdz, abc, x3d, svg, dxf, 3ds. |
| `options` | `object?` | — | `null` | Extra importer properties by name, as blender_describe_operator lists them for the importer, e.g. {"global_scale": 0.01}. |
| `timeoutSeconds` | `integer` | — | `120` | Seconds to wait for Blender, 1 to 600. |

<details><summary>JSON Schema</summary>

```json
{
  "type": "object",
  "properties": {
    "path": {
      "description": "Absolute path of the file to import.",
      "type": "string"
    },
    "format": {
      "description": "Format when the extension does not say: fbx, obj, stl, ply, gltf, glb, dae, usd, usda, usdc, usdz, abc, x3d, svg, dxf, 3ds.",
      "type": [
        "string",
        "null"
      ],
      "default": null
    },
    "options": {
      "description": "Extra importer properties by name, as blender_describe_operator lists them for the importer, e.g. {\u0022global_scale\u0022: 0.01}.",
      "type": [
        "object",
        "null"
      ],
      "default": null
    },
    "timeoutSeconds": {
      "description": "Seconds to wait for Blender, 1 to 600.",
      "type": "integer",
      "default": 120
    }
  },
  "required": [
    "path"
  ]
}
```

</details>

## <code>blender_<wbr>export_<wbr>file</code>

Exports the scene or the selected objects to a file; the format follows the extension. glb packs everything into one file.

*may overwrite or delete · every call adds · stays inside Blender.*

| Parameter | Type | Required | Default | Description |
| --- | --- | --- | --- | --- |
| `path` | `string` | yes | — | Absolute path of the file to write; folders are created. |
| `format` | `string?` | — | `null` | Format when the extension does not say: fbx, obj, stl, ply, gltf, glb, dae, usd, usda, usdc, usdz, abc, x3d, dxf, 3ds. |
| `selectedOnly` | `boolean` | — | `false` | Export only selected objects instead of the whole scene. |
| `names` | `array?` | — | `null` | With selectedOnly: select exactly these objects first. |
| `options` | `object?` | — | `null` | Extra exporter properties by name, e.g. {"apply_unit_scale": true}, {"export_apply": true}. |
| `timeoutSeconds` | `integer` | — | `120` | Seconds to wait for Blender, 1 to 600. |

<details><summary>JSON Schema</summary>

```json
{
  "type": "object",
  "properties": {
    "path": {
      "description": "Absolute path of the file to write; folders are created.",
      "type": "string"
    },
    "format": {
      "description": "Format when the extension does not say: fbx, obj, stl, ply, gltf, glb, dae, usd, usda, usdc, usdz, abc, x3d, dxf, 3ds.",
      "type": [
        "string",
        "null"
      ],
      "default": null
    },
    "selectedOnly": {
      "description": "Export only selected objects instead of the whole scene.",
      "type": "boolean",
      "default": false
    },
    "names": {
      "description": "With selectedOnly: select exactly these objects first.",
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
    "options": {
      "description": "Extra exporter properties by name, e.g. {\u0022apply_unit_scale\u0022: true}, {\u0022export_apply\u0022: true}.",
      "type": [
        "object",
        "null"
      ],
      "default": null
    },
    "timeoutSeconds": {
      "description": "Seconds to wait for Blender, 1 to 600.",
      "type": "integer",
      "default": 120
    }
  },
  "required": [
    "path"
  ]
}
```

</details>

## <code>blender_<wbr>list_<wbr>assets</code>

Lists what another .blend file holds by type: objects, collections, materials, node groups, worlds, images and more.

*read-only · every call adds · stays inside Blender.*

| Parameter | Type | Required | Default | Description |
| --- | --- | --- | --- | --- |
| `path` | `string` | yes | — | Absolute path of the .blend file. |
| `types` | `array?` | — | `null` | Types to list: objects, collections, materials, node_groups, worlds, images, meshes, actions, cameras, lights, scenes, texts; every type otherwise. |
| `assetsOnly` | `boolean` | — | `false` | Only datablocks marked as assets. |

<details><summary>JSON Schema</summary>

```json
{
  "type": "object",
  "properties": {
    "path": {
      "description": "Absolute path of the .blend file.",
      "type": "string"
    },
    "types": {
      "description": "Types to list: objects, collections, materials, node_groups, worlds, images, meshes, actions, cameras, lights, scenes, texts; every type otherwise.",
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
    "assetsOnly": {
      "description": "Only datablocks marked as assets.",
      "type": "boolean",
      "default": false
    }
  },
  "required": [
    "path"
  ]
}
```

</details>

## <code>blender_<wbr>append_<wbr>assets</code>

Appends (copies) or links objects, collections, materials, node groups, worlds or images from another .blend file into the scene, optionally into a collection; linked collections arrive as instances.

*only adds · every call adds · stays inside Blender.*

| Parameter | Type | Required | Default | Description |
| --- | --- | --- | --- | --- |
| `path` | `string` | yes | — | Absolute path of the .blend file. |
| `type` | `string` | — | `objects` | objects (the default), collections, materials, node_groups, worlds, images, meshes, actions, cameras, lights. |
| `names` | `array?` | — | `null` | Names to bring in; everything of that type otherwise. |
| `link` | `boolean` | — | `false` | Link instead of append: the data stays in the other file and updates with it. |
| `collection` | `string?` | — | `null` | Collection to place objects or collections into; created when missing. |
| `scene` | `string?` | — | `null` | Scene to act on; the current scene otherwise. |

<details><summary>JSON Schema</summary>

```json
{
  "type": "object",
  "properties": {
    "path": {
      "description": "Absolute path of the .blend file.",
      "type": "string"
    },
    "type": {
      "description": "objects (the default), collections, materials, node_groups, worlds, images, meshes, actions, cameras, lights.",
      "type": "string",
      "default": "objects"
    },
    "names": {
      "description": "Names to bring in; everything of that type otherwise.",
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
    "link": {
      "description": "Link instead of append: the data stays in the other file and updates with it.",
      "type": "boolean",
      "default": false
    },
    "collection": {
      "description": "Collection to place objects or collections into; created when missing.",
      "type": [
        "string",
        "null"
      ],
      "default": null
    },
    "scene": {
      "description": "Scene to act on; the current scene otherwise.",
      "type": [
        "string",
        "null"
      ],
      "default": null
    }
  },
  "required": [
    "path"
  ]
}
```

</details>

