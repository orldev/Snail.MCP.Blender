# Product shots, captures and bakes

Part of the `rendering` skill: these tools appear once it is loaded — by `blender_enable_skill` with `rendering`, or by `find_tool` when a search returns one of them.

## <code>blender_<wbr>render_<wbr>object</code>

Renders one object (or a few) alone: a temporary camera frames them from a chosen angle, everything else is hidden from the render, the background is transparent. A product shot in one call.

*only adds · every call adds · stays inside Blender.*

| Parameter | Type | Required | Default | Description |
| --- | --- | --- | --- | --- |
| `path` | `string` | yes | — | Absolute path of the image to write. |
| `name` | `string?` | — | `null` | Name of the object as shown in the outliner. |
| `names` | `array?` | — | `null` | Several objects to frame together instead of one. |
| `azimuth` | `number?` | — | `null` | Camera direction around the vertical axis in degrees; 0 looks from +X. |
| `elevation` | `number?` | — | `null` | Camera height angle in degrees; 0 is level, 90 is straight down. |
| `lens` | `number?` | — | `null` | Focal length in millimetres. |
| `margin` | `number?` | — | `null` | Space around the object; 1 fills the frame, 1.15 is the default. |
| `transparent` | `boolean` | — | `true` | Transparent background. |
| `resolutionX` | `integer?` | — | `null` | Pixels along this edge, 1 to 4096. |
| `resolutionY` | `integer?` | — | `null` | Pixels along this edge, 1 to 4096. |
| `keepCamera` | `boolean` | — | `false` | Keep the temporary camera (SnailShot) in the scene. |
| `preview` | `boolean` | — | `true` | Return a small JPEG of the result as image content, with exposure statistics; false for the path only. |
| `timeoutSeconds` | `integer` | — | `300` | Seconds to wait for Blender, 1 to 600. |

<details><summary>JSON Schema</summary>

```json
{
  "type": "object",
  "properties": {
    "path": {
      "description": "Absolute path of the image to write.",
      "type": "string"
    },
    "name": {
      "description": "Name of the object as shown in the outliner.",
      "type": [
        "string",
        "null"
      ],
      "default": null
    },
    "names": {
      "description": "Several objects to frame together instead of one.",
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
    "azimuth": {
      "description": "Camera direction around the vertical axis in degrees; 0 looks from \u002BX.",
      "type": [
        "number",
        "null"
      ],
      "default": null
    },
    "elevation": {
      "description": "Camera height angle in degrees; 0 is level, 90 is straight down.",
      "type": [
        "number",
        "null"
      ],
      "default": null
    },
    "lens": {
      "description": "Focal length in millimetres.",
      "type": [
        "number",
        "null"
      ],
      "default": null
    },
    "margin": {
      "description": "Space around the object; 1 fills the frame, 1.15 is the default.",
      "type": [
        "number",
        "null"
      ],
      "default": null
    },
    "transparent": {
      "description": "Transparent background.",
      "type": "boolean",
      "default": true
    },
    "resolutionX": {
      "description": "Pixels along this edge, 1 to 4096.",
      "type": [
        "integer",
        "null"
      ],
      "default": null
    },
    "resolutionY": {
      "description": "Pixels along this edge, 1 to 4096.",
      "type": [
        "integer",
        "null"
      ],
      "default": null
    },
    "keepCamera": {
      "description": "Keep the temporary camera (SnailShot) in the scene.",
      "type": "boolean",
      "default": false
    },
    "preview": {
      "description": "Return a small JPEG of the result as image content, with exposure statistics; false for the path only.",
      "type": "boolean",
      "default": true
    },
    "timeoutSeconds": {
      "description": "Seconds to wait for Blender, 1 to 600.",
      "type": "integer",
      "default": 300
    }
  },
  "required": [
    "path"
  ]
}
```

</details>

## <code>blender_<wbr>viewport_<wbr>capture</code>

Grabs the 3D viewport as the user sees it, in its current shading, to a file; fast feedback without a render. Needs Blender's window.

*only adds · every call adds · stays inside Blender.*

| Parameter | Type | Required | Default | Description |
| --- | --- | --- | --- | --- |
| `path` | `string` | yes | — | Absolute path of the image to write. |
| `preview` | `boolean` | — | `true` | Return a small JPEG of the result as image content, with exposure statistics; false for the path only. |

<details><summary>JSON Schema</summary>

```json
{
  "type": "object",
  "properties": {
    "path": {
      "description": "Absolute path of the image to write.",
      "type": "string"
    },
    "preview": {
      "description": "Return a small JPEG of the result as image content, with exposure statistics; false for the path only.",
      "type": "boolean",
      "default": true
    }
  },
  "required": [
    "path"
  ]
}
```

</details>

## <code>blender_<wbr>inspect_<wbr>image</code>

Looks at an image file: a preview the model can see, plus exposure statistics (mean luminance, clipped highlights and shadows, a histogram). For frames a background job wrote, textures, or any render on disk.

*read-only · every call adds · stays inside Blender.*

| Parameter | Type | Required | Default | Description |
| --- | --- | --- | --- | --- |
| `path` | `string` | yes | — | Absolute path of the image: PNG, JPEG, EXR, TIFF or anything Blender opens. |
| `preview` | `boolean` | — | `true` | Return a small JPEG of the result as image content, with exposure statistics; false for the path only. |
| `previewSize` | `integer?` | — | `null` | Longest edge of the preview in pixels; 512 by default, 1024 to read fine detail. |

<details><summary>JSON Schema</summary>

```json
{
  "type": "object",
  "properties": {
    "path": {
      "description": "Absolute path of the image: PNG, JPEG, EXR, TIFF or anything Blender opens.",
      "type": "string"
    },
    "preview": {
      "description": "Return a small JPEG of the result as image content, with exposure statistics; false for the path only.",
      "type": "boolean",
      "default": true
    },
    "previewSize": {
      "description": "Longest edge of the preview in pixels; 512 by default, 1024 to read fine detail.",
      "type": [
        "integer",
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

## <code>blender_<wbr>bake_<wbr>texture</code>

Bakes a material channel (colour, normal, AO, roughness, combined) into a PNG through the mesh's UVs with Cycles; the way to take procedural materials into game engines.

*only adds · every call adds · stays inside Blender.*

| Parameter | Type | Required | Default | Description |
| --- | --- | --- | --- | --- |
| `name` | `string` | yes | — | Name of the mesh object. |
| `path` | `string` | yes | — | Absolute path of the PNG to write. |
| `type` | `string` | — | `DIFFUSE` | DIFFUSE, COMBINED, AO, NORMAL, ROUGHNESS, EMIT, SHADOW, POSITION, UV, GLOSSY, TRANSMISSION or ENVIRONMENT. |
| `width` | `integer` | — | `1024` | Texture width in pixels, up to 4096. |
| `height` | `integer?` | — | `null` | Texture height in pixels; the width otherwise. |
| `samples` | `integer?` | — | `null` | Cycles samples; low values are fine for colour bakes. |
| `margin` | `integer?` | — | `null` | Pixels of bleed around UV islands. |
| `imageName` | `string?` | — | `null` | Name for the image datablock. |
| `keepNode` | `boolean` | — | `false` | Leave the image texture node in the materials. |
| `preview` | `boolean` | — | `true` | Return a small JPEG of the result as image content, with exposure statistics; false for the path only. |
| `timeoutSeconds` | `integer` | — | `600` | Seconds to wait for Blender, 1 to 600. |

<details><summary>JSON Schema</summary>

```json
{
  "type": "object",
  "properties": {
    "name": {
      "description": "Name of the mesh object.",
      "type": "string"
    },
    "path": {
      "description": "Absolute path of the PNG to write.",
      "type": "string"
    },
    "type": {
      "description": "DIFFUSE, COMBINED, AO, NORMAL, ROUGHNESS, EMIT, SHADOW, POSITION, UV, GLOSSY, TRANSMISSION or ENVIRONMENT.",
      "type": "string",
      "default": "DIFFUSE"
    },
    "width": {
      "description": "Texture width in pixels, up to 4096.",
      "type": "integer",
      "default": 1024
    },
    "height": {
      "description": "Texture height in pixels; the width otherwise.",
      "type": [
        "integer",
        "null"
      ],
      "default": null
    },
    "samples": {
      "description": "Cycles samples; low values are fine for colour bakes.",
      "type": [
        "integer",
        "null"
      ],
      "default": null
    },
    "margin": {
      "description": "Pixels of bleed around UV islands.",
      "type": [
        "integer",
        "null"
      ],
      "default": null
    },
    "imageName": {
      "description": "Name for the image datablock.",
      "type": [
        "string",
        "null"
      ],
      "default": null
    },
    "keepNode": {
      "description": "Leave the image texture node in the materials.",
      "type": "boolean",
      "default": false
    },
    "preview": {
      "description": "Return a small JPEG of the result as image content, with exposure statistics; false for the path only.",
      "type": "boolean",
      "default": true
    },
    "timeoutSeconds": {
      "description": "Seconds to wait for Blender, 1 to 600.",
      "type": "integer",
      "default": 600
    }
  },
  "required": [
    "name",
    "path"
  ]
}
```

</details>

