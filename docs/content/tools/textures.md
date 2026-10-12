# Textures and UVs

Part of the `materials` skill: these tools appear once it is loaded — by `blender_enable_skill` with `materials`, or by `find_tool` when a search returns one of them.

## <code>blender_<wbr>load_<wbr>image_<wbr>texture</code>

Loads an image file into an Image Texture node and wires it into the Principled BSDF. The mesh needs UVs: blender_unwrap_uv makes them.

*only adds · every call adds · stays inside Blender.*

| Parameter | Type | Required | Default | Description |
| --- | --- | --- | --- | --- |
| `material` | `string` | yes | — | Material name as blender_list_materials shows it. |
| `path` | `string` | yes | — | Absolute path of the image file. |
| `name` | `string?` | — | `null` | Name for the image node. |
| `connectTo` | `string?` | — | `Base Color` | Principled BSDF input to wire the texture into: Base Color, Roughness, Metallic, Alpha, Normal (adds a bump node) or Emission Color; empty leaves it unconnected. |
| `colorspace` | `string?` | — | `null` | Colour space: sRGB for colour images, Non-Color for roughness, normal and height maps. |
| `projection` | `string?` | — | `null` | FLAT (UV), BOX, SPHERE or TUBE projection. |

<details><summary>JSON Schema</summary>

```json
{
  "type": "object",
  "properties": {
    "material": {
      "description": "Material name as blender_list_materials shows it.",
      "type": "string"
    },
    "path": {
      "description": "Absolute path of the image file.",
      "type": "string"
    },
    "name": {
      "description": "Name for the image node.",
      "type": [
        "string",
        "null"
      ],
      "default": null
    },
    "connectTo": {
      "description": "Principled BSDF input to wire the texture into: Base Color, Roughness, Metallic, Alpha, Normal (adds a bump node) or Emission Color; empty leaves it unconnected.",
      "type": [
        "string",
        "null"
      ],
      "default": "Base Color"
    },
    "colorspace": {
      "description": "Colour space: sRGB for colour images, Non-Color for roughness, normal and height maps.",
      "type": [
        "string",
        "null"
      ],
      "default": null
    },
    "projection": {
      "description": "FLAT (UV), BOX, SPHERE or TUBE projection.",
      "type": [
        "string",
        "null"
      ],
      "default": null
    }
  },
  "required": [
    "material",
    "path"
  ]
}
```

</details>

## <code>blender_<wbr>procedural_<wbr>texture</code>

Adds a procedural texture (noise, voronoi, brick and the rest), optionally through a colour ramp, and wires it into a Principled BSDF input; Normal goes through a bump node.

*only adds · every call adds · stays inside Blender.*

| Parameter | Type | Required | Default | Description |
| --- | --- | --- | --- | --- |
| `material` | `string` | yes | — | Material name as blender_list_materials shows it. |
| `kind` | `string` | — | `noise` | noise, voronoi, musgrave, magic, wave, brick, checker or gradient. |
| `name` | `string?` | — | `null` | Name for the texture node. |
| `inputs` | `object?` | — | `null` | Texture inputs by socket name, e.g. {"Scale": 5, "Detail": 8, "Roughness": 0.6}. |
| `properties` | `object?` | — | `null` | Non-socket settings by Python name, e.g. {"noise_dimensions": "4D"}, {"blend_type": "MULTIPLY"}, {"operation": "ADD"}. |
| `connectTo` | `string?` | — | `Base Color` | Principled BSDF input to wire the texture into: Base Color, Roughness, Metallic, Alpha, Normal (adds a bump node) or Emission Color; empty leaves it unconnected. |
| `colorRamp` | `array?` | — | `null` | Insert a colour ramp: [{"position": 0, "color": [r, g, b]}, ...] with stops, or [] for a default black-to-white ramp. |
| `bumpStrength` | `number?` | — | `null` | Bump strength when connecting to Normal. |

<details><summary>JSON Schema</summary>

```json
{
  "type": "object",
  "properties": {
    "material": {
      "description": "Material name as blender_list_materials shows it.",
      "type": "string"
    },
    "kind": {
      "description": "noise, voronoi, musgrave, magic, wave, brick, checker or gradient.",
      "type": "string",
      "default": "noise"
    },
    "name": {
      "description": "Name for the texture node.",
      "type": [
        "string",
        "null"
      ],
      "default": null
    },
    "inputs": {
      "description": "Texture inputs by socket name, e.g. {\u0022Scale\u0022: 5, \u0022Detail\u0022: 8, \u0022Roughness\u0022: 0.6}.",
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
    "connectTo": {
      "description": "Principled BSDF input to wire the texture into: Base Color, Roughness, Metallic, Alpha, Normal (adds a bump node) or Emission Color; empty leaves it unconnected.",
      "type": [
        "string",
        "null"
      ],
      "default": "Base Color"
    },
    "colorRamp": {
      "description": "Insert a colour ramp: [{\u0022position\u0022: 0, \u0022color\u0022: [r, g, b]}, ...] with stops, or [] for a default black-to-white ramp.",
      "type": [
        "array",
        "null"
      ],
      "default": null
    },
    "bumpStrength": {
      "description": "Bump strength when connecting to Normal.",
      "type": [
        "number",
        "null"
      ],
      "default": null
    }
  },
  "required": [
    "material"
  ]
}
```

</details>

## <code>blender_<wbr>unwrap_<wbr>uv</code>

Unwraps a mesh's UVs by Smart UV Project, seams, cube, sphere or cylinder projection.

*only adds · every call adds · stays inside Blender.*

| Parameter | Type | Required | Default | Description |
| --- | --- | --- | --- | --- |
| `name` | `string` | yes | — | Name of the mesh object. |
| `method` | `string` | — | `smart` | smart (Smart UV Project, the default), unwrap (needs seams), cube, sphere or cylinder. |
| `margin` | `number` | — | `0.02` | Space between UV islands, 0 to 1. |
| `angleLimit` | `number?` | — | `null` | smart: faces meeting at a sharper angle in degrees start a new island. |

<details><summary>JSON Schema</summary>

```json
{
  "type": "object",
  "properties": {
    "name": {
      "description": "Name of the mesh object.",
      "type": "string"
    },
    "method": {
      "description": "smart (Smart UV Project, the default), unwrap (needs seams), cube, sphere or cylinder.",
      "type": "string",
      "default": "smart"
    },
    "margin": {
      "description": "Space between UV islands, 0 to 1.",
      "type": "number",
      "default": 0.02
    },
    "angleLimit": {
      "description": "smart: faces meeting at a sharper angle in degrees start a new island.",
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

