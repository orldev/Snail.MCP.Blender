# Materials

Part of the `materials` skill: these tools appear once it is loaded — by `blender_enable_skill` with `materials`, or by `find_tool` when a search returns one of them.

## <code>blender_<wbr>create_<wbr>material</code>

Creates a node material with Principled BSDF settings and optionally assigns it to an object. Every value is optional; the rest keep Blender's defaults. Returns the effective settings.

*only adds · every call adds · stays inside Blender.*

| Parameter | Type | Required | Default | Description |
| --- | --- | --- | --- | --- |
| `name` | `string?` | — | `null` | Name of the new material. |
| `assignTo` | `string?` | — | `null` | Object to assign it to right away. |
| `baseColor` | `array?` | — | `null` | [r, g, b] or [r, g, b, a] with components 0 to 1. |
| `metallic` | `number?` | — | `null` | 0 dielectric to 1 metal. |
| `roughness` | `number?` | — | `null` | 0 mirror to 1 matte. |
| `ior` | `number?` | — | `null` | Index of refraction; 1.45 glass, 1.33 water. |
| `alpha` | `number?` | — | `null` | Opacity 0 to 1; below 1 needs a blend method. |
| `emissionColor` | `array?` | — | `null` | [r, g, b] emission colour; pair with emissionStrength above 0. |
| `emissionStrength` | `number?` | — | `null` | Emission strength; 0 is off. |
| `transmission` | `number?` | — | `null` | Transmission 0 to 1; 1 for glass. |
| `subsurface` | `number?` | — | `null` | Subsurface scattering weight 0 to 1. |
| `coat` | `number?` | — | `null` | Coat (clearcoat) weight 0 to 1. |
| `sheen` | `number?` | — | `null` | Sheen weight 0 to 1. |
| `specular` | `number?` | — | `null` | Specular level 0 to 1; 0.5 is the default. |
| `blendMethod` | `string?` | — | `null` | How alpha renders: OPAQUE, CLIP, HASHED, BLEND. |

<details><summary>JSON Schema</summary>

```json
{
  "type": "object",
  "properties": {
    "name": {
      "description": "Name of the new material.",
      "type": [
        "string",
        "null"
      ],
      "default": null
    },
    "assignTo": {
      "description": "Object to assign it to right away.",
      "type": [
        "string",
        "null"
      ],
      "default": null
    },
    "baseColor": {
      "description": "[r, g, b] or [r, g, b, a] with components 0 to 1.",
      "type": [
        "array",
        "null"
      ],
      "items": {
        "type": "number"
      },
      "default": null
    },
    "metallic": {
      "description": "0 dielectric to 1 metal.",
      "type": [
        "number",
        "null"
      ],
      "default": null
    },
    "roughness": {
      "description": "0 mirror to 1 matte.",
      "type": [
        "number",
        "null"
      ],
      "default": null
    },
    "ior": {
      "description": "Index of refraction; 1.45 glass, 1.33 water.",
      "type": [
        "number",
        "null"
      ],
      "default": null
    },
    "alpha": {
      "description": "Opacity 0 to 1; below 1 needs a blend method.",
      "type": [
        "number",
        "null"
      ],
      "default": null
    },
    "emissionColor": {
      "description": "[r, g, b] emission colour; pair with emissionStrength above 0.",
      "type": [
        "array",
        "null"
      ],
      "items": {
        "type": "number"
      },
      "default": null
    },
    "emissionStrength": {
      "description": "Emission strength; 0 is off.",
      "type": [
        "number",
        "null"
      ],
      "default": null
    },
    "transmission": {
      "description": "Transmission 0 to 1; 1 for glass.",
      "type": [
        "number",
        "null"
      ],
      "default": null
    },
    "subsurface": {
      "description": "Subsurface scattering weight 0 to 1.",
      "type": [
        "number",
        "null"
      ],
      "default": null
    },
    "coat": {
      "description": "Coat (clearcoat) weight 0 to 1.",
      "type": [
        "number",
        "null"
      ],
      "default": null
    },
    "sheen": {
      "description": "Sheen weight 0 to 1.",
      "type": [
        "number",
        "null"
      ],
      "default": null
    },
    "specular": {
      "description": "Specular level 0 to 1; 0.5 is the default.",
      "type": [
        "number",
        "null"
      ],
      "default": null
    },
    "blendMethod": {
      "description": "How alpha renders: OPAQUE, CLIP, HASHED, BLEND.",
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

## <code>blender_<wbr>set_<wbr>material</code>

Changes Principled BSDF settings of an existing material, or renames it. It edits the material itself and touches no object: putting a material on an object is blender_assign_material.

*only adds · every call adds · stays inside Blender.*

| Parameter | Type | Required | Default | Description |
| --- | --- | --- | --- | --- |
| `name` | `string` | yes | — | Material name as blender_list_materials shows it. |
| `newName` | `string?` | — | `null` | Rename the material. |
| `baseColor` | `array?` | — | `null` | [r, g, b] or [r, g, b, a] with components 0 to 1. |
| `metallic` | `number?` | — | `null` | 0 dielectric to 1 metal. |
| `roughness` | `number?` | — | `null` | 0 mirror to 1 matte. |
| `ior` | `number?` | — | `null` | Index of refraction; 1.45 glass, 1.33 water. |
| `alpha` | `number?` | — | `null` | Opacity 0 to 1; below 1 needs a blend method. |
| `emissionColor` | `array?` | — | `null` | [r, g, b] emission colour; pair with emissionStrength above 0. |
| `emissionStrength` | `number?` | — | `null` | Emission strength; 0 is off. |
| `transmission` | `number?` | — | `null` | Transmission 0 to 1; 1 for glass. |
| `subsurface` | `number?` | — | `null` | Subsurface scattering weight 0 to 1. |
| `coat` | `number?` | — | `null` | Coat (clearcoat) weight 0 to 1. |
| `sheen` | `number?` | — | `null` | Sheen weight 0 to 1. |
| `specular` | `number?` | — | `null` | Specular level 0 to 1; 0.5 is the default. |
| `blendMethod` | `string?` | — | `null` | How alpha renders: OPAQUE, CLIP, HASHED, BLEND. |

<details><summary>JSON Schema</summary>

```json
{
  "type": "object",
  "properties": {
    "name": {
      "description": "Material name as blender_list_materials shows it.",
      "type": "string"
    },
    "newName": {
      "description": "Rename the material.",
      "type": [
        "string",
        "null"
      ],
      "default": null
    },
    "baseColor": {
      "description": "[r, g, b] or [r, g, b, a] with components 0 to 1.",
      "type": [
        "array",
        "null"
      ],
      "items": {
        "type": "number"
      },
      "default": null
    },
    "metallic": {
      "description": "0 dielectric to 1 metal.",
      "type": [
        "number",
        "null"
      ],
      "default": null
    },
    "roughness": {
      "description": "0 mirror to 1 matte.",
      "type": [
        "number",
        "null"
      ],
      "default": null
    },
    "ior": {
      "description": "Index of refraction; 1.45 glass, 1.33 water.",
      "type": [
        "number",
        "null"
      ],
      "default": null
    },
    "alpha": {
      "description": "Opacity 0 to 1; below 1 needs a blend method.",
      "type": [
        "number",
        "null"
      ],
      "default": null
    },
    "emissionColor": {
      "description": "[r, g, b] emission colour; pair with emissionStrength above 0.",
      "type": [
        "array",
        "null"
      ],
      "items": {
        "type": "number"
      },
      "default": null
    },
    "emissionStrength": {
      "description": "Emission strength; 0 is off.",
      "type": [
        "number",
        "null"
      ],
      "default": null
    },
    "transmission": {
      "description": "Transmission 0 to 1; 1 for glass.",
      "type": [
        "number",
        "null"
      ],
      "default": null
    },
    "subsurface": {
      "description": "Subsurface scattering weight 0 to 1.",
      "type": [
        "number",
        "null"
      ],
      "default": null
    },
    "coat": {
      "description": "Coat (clearcoat) weight 0 to 1.",
      "type": [
        "number",
        "null"
      ],
      "default": null
    },
    "sheen": {
      "description": "Sheen weight 0 to 1.",
      "type": [
        "number",
        "null"
      ],
      "default": null
    },
    "specular": {
      "description": "Specular level 0 to 1; 0.5 is the default.",
      "type": [
        "number",
        "null"
      ],
      "default": null
    },
    "blendMethod": {
      "description": "How alpha renders: OPAQUE, CLIP, HASHED, BLEND.",
      "type": [
        "string",
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

## <code>blender_<wbr>assign_<wbr>material</code>

Assigns a material to an object: by default the object becomes that material, which fills slot 0 and points every face at it. A given slot is filled instead, and the faces still follow. selectedFaces gives the material a slot of its own and points only the mesh's selected faces at it; append adds the slot and leaves the faces alone. The reply names the slot, the slot list and how many faces now point at it.

*only adds · every call adds · stays inside Blender.*

| Parameter | Type | Required | Default | Description |
| --- | --- | --- | --- | --- |
| `object` | `string` | yes | — | Name of the object as shown in the outliner. |
| `material` | `string` | yes | — | Material name as blender_list_materials shows it. |
| `slot` | `integer?` | — | `null` | Material slot index to fill; slot 0 otherwise, and the faces follow it there. |
| `selectedFaces` | `boolean` | — | `false` | Give the slot only to the mesh's selected faces (blender_mesh_select) instead of the whole object; this adds a slot rather than replacing one. |
| `append` | `boolean` | — | `false` | Add a slot at the end and leave the faces where they are, for building a mesh that wears several materials. |

<details><summary>JSON Schema</summary>

```json
{
  "type": "object",
  "properties": {
    "object": {
      "description": "Name of the object as shown in the outliner.",
      "type": "string"
    },
    "material": {
      "description": "Material name as blender_list_materials shows it.",
      "type": "string"
    },
    "slot": {
      "description": "Material slot index to fill; slot 0 otherwise, and the faces follow it there.",
      "type": [
        "integer",
        "null"
      ],
      "default": null
    },
    "selectedFaces": {
      "description": "Give the slot only to the mesh\u0027s selected faces (blender_mesh_select) instead of the whole object; this adds a slot rather than replacing one.",
      "type": "boolean",
      "default": false
    },
    "append": {
      "description": "Add a slot at the end and leave the faces where they are, for building a mesh that wears several materials.",
      "type": "boolean",
      "default": false
    }
  },
  "required": [
    "object",
    "material"
  ]
}
```

</details>

## <code>blender_<wbr>list_<wbr>materials</code>

Every material in the file with its user count.

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

## <code>blender_<wbr>material_<wbr>info</code>

A material in depth: Principled settings and the full node graph with node types, input values and links.

*read-only · every call adds · stays inside Blender.*

| Parameter | Type | Required | Default | Description |
| --- | --- | --- | --- | --- |
| `name` | `string` | yes | — | Material name as blender_list_materials shows it. |

<details><summary>JSON Schema</summary>

```json
{
  "type": "object",
  "properties": {
    "name": {
      "description": "Material name as blender_list_materials shows it.",
      "type": "string"
    }
  },
  "required": [
    "name"
  ]
}
```

</details>

## <code>blender_<wbr>set_<wbr>viewport_<wbr>shading</code>

Switches every 3D viewport to wireframe, solid, material preview or rendered shading.

*only adds · every call adds · stays inside Blender.*

| Parameter | Type | Required | Default | Description |
| --- | --- | --- | --- | --- |
| `mode` | `string` | — | `MATERIAL` | WIREFRAME, SOLID, MATERIAL or RENDERED. |

<details><summary>JSON Schema</summary>

```json
{
  "type": "object",
  "properties": {
    "mode": {
      "description": "WIREFRAME, SOLID, MATERIAL or RENDERED.",
      "type": "string",
      "default": "MATERIAL"
    }
  }
}
```

</details>

