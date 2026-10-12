# Camera optics

Part of the `rendering` skill: these tools appear once it is loaded — by `blender_enable_skill` with `rendering`, or by `find_tool` when a search returns one of them.

## <code>blender_<wbr>set_<wbr>camera_<wbr>optics</code>

The physical camera: sensor size and fit, focal length or field of view, lens shift, clip range, perspective, orthographic or panoramic type, aperture with f-stop, blade count, rotation and anamorphic ratio, focus by distance, object or bone. Returns the optics and the hyperfocal distance. Motion blur lives in blender_set_motion_blur and, per object, in blender_object_render_flags.

*only adds · every call adds · stays inside Blender.*

| Parameter | Type | Required | Default | Description |
| --- | --- | --- | --- | --- |
| `camera` | `string?` | — | `null` | Camera object; the scene camera otherwise. |
| `sensor` | `object?` | — | `null` | Sensor size and fit. |
| `lens` | `number?` | — | `null` | Focal length in mm. |
| `fov` | `number?` | — | `null` | Horizontal field of view in degrees instead of the focal length. |
| `shift` | `array?` | — | `null` | Lens shift [x, y] as fractions of the frame; keeps verticals parallel without tilting the camera. |
| `clip` | `array?` | — | `null` | Clip range [start, end] in metres. |
| `type` | `string?` | — | `null` | PERSP, ORTHO or PANO. |
| `panorama` | `string?` | — | `null` | Panoramic projection, which also sets the type to PANO: EQUIRECTANGULAR, FISHEYE_EQUIDISTANT, FISHEYE_EQUISOLID, FISHEYE_LENS_POLYNOMIAL, CENTRAL_CYLINDRICAL. |
| `orthoScale` | `number?` | — | `null` | Orthographic scale in metres. |
| `aperture` | `object?` | — | `null` | Aperture: f-stop, blades, rotation, anamorphic ratio. |
| `focus` | `object?` | — | `null` | Focus by distance, object or bone. |
| `scene` | `string?` | — | `null` | Scene to act on; the current scene otherwise. |

<details><summary>JSON Schema</summary>

```json
{
  "type": "object",
  "properties": {
    "camera": {
      "description": "Camera object; the scene camera otherwise.",
      "type": [
        "string",
        "null"
      ],
      "default": null
    },
    "sensor": {
      "description": "Sensor size and fit.",
      "type": [
        "object",
        "null"
      ],
      "properties": {
        "width": {
          "description": "Width in mm: 36 full frame, 23.5 APS-C, 24.89 Super 35, 6.17 phone.",
          "type": [
            "number",
            "null"
          ]
        },
        "height": {
          "description": "Height in mm; 24 for full frame.",
          "type": [
            "number",
            "null"
          ]
        },
        "fit": {
          "description": "AUTO, HORIZONTAL or VERTICAL: which sensor edge the field of view follows.",
          "type": [
            "string",
            "null"
          ]
        }
      },
      "default": null
    },
    "lens": {
      "description": "Focal length in mm.",
      "type": [
        "number",
        "null"
      ],
      "default": null
    },
    "fov": {
      "description": "Horizontal field of view in degrees instead of the focal length.",
      "type": [
        "number",
        "null"
      ],
      "default": null
    },
    "shift": {
      "description": "Lens shift [x, y] as fractions of the frame; keeps verticals parallel without tilting the camera.",
      "type": [
        "array",
        "null"
      ],
      "items": {
        "type": "number"
      },
      "default": null
    },
    "clip": {
      "description": "Clip range [start, end] in metres.",
      "type": [
        "array",
        "null"
      ],
      "items": {
        "type": "number"
      },
      "default": null
    },
    "type": {
      "description": "PERSP, ORTHO or PANO.",
      "type": [
        "string",
        "null"
      ],
      "default": null
    },
    "panorama": {
      "description": "Panoramic projection, which also sets the type to PANO: EQUIRECTANGULAR, FISHEYE_EQUIDISTANT, FISHEYE_EQUISOLID, FISHEYE_LENS_POLYNOMIAL, CENTRAL_CYLINDRICAL.",
      "type": [
        "string",
        "null"
      ],
      "default": null
    },
    "orthoScale": {
      "description": "Orthographic scale in metres.",
      "type": [
        "number",
        "null"
      ],
      "default": null
    },
    "aperture": {
      "description": "Aperture: f-stop, blades, rotation, anamorphic ratio.",
      "type": [
        "object",
        "null"
      ],
      "properties": {
        "enabled": {
          "description": "Depth of field on or off; on when any other field is given.",
          "type": [
            "boolean",
            "null"
          ]
        },
        "fstop": {
          "description": "f-number: 1.4 shallow, 8 deep.",
          "type": [
            "number",
            "null"
          ]
        },
        "blades": {
          "description": "Iris blades: 0 is a perfect circle, 5 to 9 give polygonal bokeh.",
          "type": [
            "integer",
            "null"
          ]
        },
        "rotation": {
          "description": "Blade rotation in degrees.",
          "type": [
            "number",
            "null"
          ]
        },
        "ratio": {
          "description": "Anamorphic squeeze: 1 spherical, 2 for 2x anamorphic bokeh.",
          "type": [
            "number",
            "null"
          ]
        }
      },
      "default": null
    },
    "focus": {
      "description": "Focus by distance, object or bone.",
      "type": [
        "object",
        "null"
      ],
      "properties": {
        "distance": {
          "description": "Focus distance in metres; cleared when an object is given.",
          "type": [
            "number",
            "null"
          ]
        },
        "object": {
          "description": "Object to keep in focus.",
          "type": [
            "string",
            "null"
          ]
        },
        "bone": {
          "description": "Bone of the focus object, e.g. an eye, for rigs.",
          "type": [
            "string",
            "null"
          ]
        }
      },
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
  }
}
```

</details>

