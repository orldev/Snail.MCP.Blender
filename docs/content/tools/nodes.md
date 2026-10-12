# Shader nodes

Part of the `materials` skill: these tools appear once it is loaded — by `blender_enable_skill` with `materials`, or by `find_tool` when a search returns one of them.

## <code>blender_<wbr>add_<wbr>node</code>

Adds a shader node to a material with input values and settings; link it with blender_link_nodes.

*only adds · every call adds · stays inside Blender.*

| Parameter | Type | Required | Default | Description |
| --- | --- | --- | --- | --- |
| `material` | `string` | yes | — | Material name as blender_list_materials shows it. |
| `type` | `string` | yes | — | Shader node bl_idname or its suffix: TexNoise, TexVoronoi, TexImage, ValToRGB (colour ramp), Bump, NormalMap, Math, Mix, MixRGB, RGBCurve, Mapping, TexCoord, SeparateXYZ, CombineXYZ, BsdfPrincipled, Emission, BsdfGlass, MixShader. |
| `name` | `string?` | — | `null` | Name for the node; Blender's default otherwise. |
| `inputs` | `object?` | — | `null` | Unlinked input sockets by name, e.g. {"Scale": 5, "Base Color": [1, 0, 0]}. |
| `properties` | `object?` | — | `null` | Non-socket settings by Python name, e.g. {"noise_dimensions": "4D"}, {"blend_type": "MULTIPLY"}, {"operation": "ADD"}. |
| `location` | `array?` | — | `null` | [x, y] position in the node editor. |

<details><summary>JSON Schema</summary>

```json
{
  "type": "object",
  "properties": {
    "material": {
      "description": "Material name as blender_list_materials shows it.",
      "type": "string"
    },
    "type": {
      "description": "Shader node bl_idname or its suffix: TexNoise, TexVoronoi, TexImage, ValToRGB (colour ramp), Bump, NormalMap, Math, Mix, MixRGB, RGBCurve, Mapping, TexCoord, SeparateXYZ, CombineXYZ, BsdfPrincipled, Emission, BsdfGlass, MixShader.",
      "type": "string"
    },
    "name": {
      "description": "Name for the node; Blender\u0027s default otherwise.",
      "type": [
        "string",
        "null"
      ],
      "default": null
    },
    "inputs": {
      "description": "Unlinked input sockets by name, e.g. {\u0022Scale\u0022: 5, \u0022Base Color\u0022: [1, 0, 0]}.",
      "type": [
        "object",
        "null"
      ],
      "default": null
    },
    "properties": {
      "description": "Non-socket settings by Python name, e.g. {\u0022noise_dimensions\u0022: \u00224D\u0022}, {\u0022blend_type\u0022: \u0022MULTIPLY\u0022}, {\u0022operation\u0022: \u0022ADD\u0022}.",
      "type": [
        "object",
        "null"
      ],
      "default": null
    },
    "location": {
      "description": "[x, y] position in the node editor.",
      "type": [
        "array",
        "null"
      ],
      "items": {
        "type": "number"
      },
      "default": null
    }
  },
  "required": [
    "material",
    "type"
  ]
}
```

</details>

## <code>blender_<wbr>set_<wbr>node</code>

Changes input values, settings, name or label of a node.

*only adds · every call adds · stays inside Blender.*

| Parameter | Type | Required | Default | Description |
| --- | --- | --- | --- | --- |
| `material` | `string` | yes | — | Material name as blender_list_materials shows it. |
| `node` | `string` | yes | — | Node name as blender_material_info lists it, e.g. Principled BSDF. |
| `inputs` | `object?` | — | `null` | Unlinked input sockets by name, e.g. {"Scale": 5, "Base Color": [1, 0, 0]}. |
| `properties` | `object?` | — | `null` | Non-socket settings by Python name, e.g. {"noise_dimensions": "4D"}, {"blend_type": "MULTIPLY"}, {"operation": "ADD"}. |
| `newName` | `string?` | — | `null` | Rename the node. |
| `label` | `string?` | — | `null` | Label shown on the node. |

<details><summary>JSON Schema</summary>

```json
{
  "type": "object",
  "properties": {
    "material": {
      "description": "Material name as blender_list_materials shows it.",
      "type": "string"
    },
    "node": {
      "description": "Node name as blender_material_info lists it, e.g. Principled BSDF.",
      "type": "string"
    },
    "inputs": {
      "description": "Unlinked input sockets by name, e.g. {\u0022Scale\u0022: 5, \u0022Base Color\u0022: [1, 0, 0]}.",
      "type": [
        "object",
        "null"
      ],
      "default": null
    },
    "properties": {
      "description": "Non-socket settings by Python name, e.g. {\u0022noise_dimensions\u0022: \u00224D\u0022}, {\u0022blend_type\u0022: \u0022MULTIPLY\u0022}, {\u0022operation\u0022: \u0022ADD\u0022}.",
      "type": [
        "object",
        "null"
      ],
      "default": null
    },
    "newName": {
      "description": "Rename the node.",
      "type": [
        "string",
        "null"
      ],
      "default": null
    },
    "label": {
      "description": "Label shown on the node.",
      "type": [
        "string",
        "null"
      ],
      "default": null
    }
  },
  "required": [
    "material",
    "node"
  ]
}
```

</details>

## <code>blender_<wbr>link_<wbr>nodes</code>

Connects an output socket of one node to an input socket of another, replacing any existing link there.

*only adds · every call adds · stays inside Blender.*

| Parameter | Type | Required | Default | Description |
| --- | --- | --- | --- | --- |
| `material` | `string` | yes | — | Material name as blender_list_materials shows it. |
| `fromNode` | `string` | yes | — | Source node name. |
| `fromSocket` | `string` | yes | — | Output socket name on the source, e.g. Color, Fac, BSDF. |
| `toNode` | `string` | yes | — | Target node name. |
| `toSocket` | `string` | yes | — | Input socket name on the target, e.g. Base Color, Roughness, Surface. |

<details><summary>JSON Schema</summary>

```json
{
  "type": "object",
  "properties": {
    "material": {
      "description": "Material name as blender_list_materials shows it.",
      "type": "string"
    },
    "fromNode": {
      "description": "Source node name.",
      "type": "string"
    },
    "fromSocket": {
      "description": "Output socket name on the source, e.g. Color, Fac, BSDF.",
      "type": "string"
    },
    "toNode": {
      "description": "Target node name.",
      "type": "string"
    },
    "toSocket": {
      "description": "Input socket name on the target, e.g. Base Color, Roughness, Surface.",
      "type": "string"
    }
  },
  "required": [
    "material",
    "fromNode",
    "fromSocket",
    "toNode",
    "toSocket"
  ]
}
```

</details>

## <code>blender_<wbr>remove_<wbr>node</code>

Deletes a node and its links.

*may overwrite or delete · every call adds · stays inside Blender.*

| Parameter | Type | Required | Default | Description |
| --- | --- | --- | --- | --- |
| `material` | `string` | yes | — | Material name as blender_list_materials shows it. |
| `node` | `string` | yes | — | Node name as blender_material_info lists it, e.g. Principled BSDF. |

<details><summary>JSON Schema</summary>

```json
{
  "type": "object",
  "properties": {
    "material": {
      "description": "Material name as blender_list_materials shows it.",
      "type": "string"
    },
    "node": {
      "description": "Node name as blender_material_info lists it, e.g. Principled BSDF.",
      "type": "string"
    }
  },
  "required": [
    "material",
    "node"
  ]
}
```

</details>

## <code>blender_<wbr>build_<wbr>node_<wbr>graph</code>

Builds several nodes and their links in one call from a specification; the way to assemble a whole shader (texture → ramp → bump → Principled) at once.

*only adds · every call adds · stays inside Blender.*

| Parameter | Type | Required | Default | Description |
| --- | --- | --- | --- | --- |
| `material` | `string` | yes | — | Material name as blender_list_materials shows it. |
| `nodes` | `array` | yes | — | Nodes as [{"id": "noise", "type": "TexNoise", "inputs": {"Scale": 8}, "properties": {}, "location": [-600, 0]}, ...]; id is how links name the node. |
| `links` | `array?` | — | `null` | Links as [["noise", "Fac", "ramp", "Fac"], ...]: from id, output socket, to id, input socket. Existing node names work as ids too, e.g. "Principled BSDF". |
| `clear` | `boolean` | — | `false` | Delete the existing nodes first; the material output and Principled BSDF must then be part of the spec. |

<details><summary>JSON Schema</summary>

```json
{
  "type": "object",
  "properties": {
    "material": {
      "description": "Material name as blender_list_materials shows it.",
      "type": "string"
    },
    "nodes": {
      "description": "Nodes as [{\u0022id\u0022: \u0022noise\u0022, \u0022type\u0022: \u0022TexNoise\u0022, \u0022inputs\u0022: {\u0022Scale\u0022: 8}, \u0022properties\u0022: {}, \u0022location\u0022: [-600, 0]}, ...]; id is how links name the node.",
      "type": "array"
    },
    "links": {
      "description": "Links as [[\u0022noise\u0022, \u0022Fac\u0022, \u0022ramp\u0022, \u0022Fac\u0022], ...]: from id, output socket, to id, input socket. Existing node names work as ids too, e.g. \u0022Principled BSDF\u0022.",
      "type": [
        "array",
        "null"
      ],
      "default": null
    },
    "clear": {
      "description": "Delete the existing nodes first; the material output and Principled BSDF must then be part of the spec.",
      "type": "boolean",
      "default": false
    }
  },
  "required": [
    "material",
    "nodes"
  ]
}
```

</details>

