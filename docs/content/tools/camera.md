# Camera

## <code>blender_<wbr>add_<wbr>camera</code>

Adds a camera, optionally aimed at a point, and makes it the render camera. Defaults to Blender's starting camera placement.

*only adds · every call adds · stays inside Blender.*

| Parameter | Type | Required | Default | Description |
| --- | --- | --- | --- | --- |
| `name` | `string?` | — | `null` | Name for the camera object. |
| `location` | `array?` | — | `null` | [x, y, z] in metres. |
| `rotation` | `array?` | — | `null` | [x, y, z] Euler rotation in degrees. |
| `lens` | `number?` | — | `null` | Focal length in millimetres. |
| `lookAt` | `array?` | — | `null` | [x, y, z] point the camera turns towards; overrides rotation. |
| `makeActive` | `boolean` | — | `true` | Make it the scene's render camera. |

<details><summary>JSON Schema</summary>

```json
{
  "type": "object",
  "properties": {
    "name": {
      "description": "Name for the camera object.",
      "type": [
        "string",
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
    "lens": {
      "description": "Focal length in millimetres.",
      "type": [
        "number",
        "null"
      ],
      "default": null
    },
    "lookAt": {
      "description": "[x, y, z] point the camera turns towards; overrides rotation.",
      "type": [
        "array",
        "null"
      ],
      "items": {
        "type": "number"
      },
      "default": null
    },
    "makeActive": {
      "description": "Make it the scene\u0027s render camera.",
      "type": "boolean",
      "default": true
    }
  }
}
```

</details>

## <code>blender_<wbr>set_<wbr>camera</code>

Changes a camera: focal length, sensor, clipping, aim point, and whether it renders; the iris and focus live in blender_set_camera_optics.

*only adds · every call adds · stays inside Blender.*

| Parameter | Type | Required | Default | Description |
| --- | --- | --- | --- | --- |
| `name` | `string` | yes | — | Name of the camera object. |
| `lens` | `number?` | — | `null` | Focal length in millimetres. |
| `sensorWidth` | `number?` | — | `null` | Sensor width in millimetres. |
| `clipStart` | `number?` | — | `null` | Near clipping distance in metres. |
| `clipEnd` | `number?` | — | `null` | Far clipping distance in metres. |
| `lookAt` | `array?` | — | `null` | [x, y, z] point the camera turns towards. |
| `makeActive` | `boolean` | — | `false` | Make it the scene's render camera. |

<details><summary>JSON Schema</summary>

```json
{
  "type": "object",
  "properties": {
    "name": {
      "description": "Name of the camera object.",
      "type": "string"
    },
    "lens": {
      "description": "Focal length in millimetres.",
      "type": [
        "number",
        "null"
      ],
      "default": null
    },
    "sensorWidth": {
      "description": "Sensor width in millimetres.",
      "type": [
        "number",
        "null"
      ],
      "default": null
    },
    "clipStart": {
      "description": "Near clipping distance in metres.",
      "type": [
        "number",
        "null"
      ],
      "default": null
    },
    "clipEnd": {
      "description": "Far clipping distance in metres.",
      "type": [
        "number",
        "null"
      ],
      "default": null
    },
    "lookAt": {
      "description": "[x, y, z] point the camera turns towards.",
      "type": [
        "array",
        "null"
      ],
      "items": {
        "type": "number"
      },
      "default": null
    },
    "makeActive": {
      "description": "Make it the scene\u0027s render camera.",
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

