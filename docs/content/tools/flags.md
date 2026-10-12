# Object render flags

Part of the `rendering` skill: these tools appear once it is loaded — by `blender_enable_skill` with `rendering`, or by `find_tool` when a search returns one of them.

## <code>blender_<wbr>object_<wbr>render_<wbr>flags</code>

Per-object rendering flags for integration and passes: holdout, shadow catcher, visibility per ray type, pass index for the object index pass, light group, caustics role, per-object motion blur, shadow terminator offset. Objects or whole collections. Returns each object's effective flags.

*only adds · every call adds · stays inside Blender.*

| Parameter | Type | Required | Default | Description |
| --- | --- | --- | --- | --- |
| `names` | `array` | yes | — | Objects, or collection names to apply to every object inside. |
| `holdout` | `boolean?` | — | `null` | Cut a transparent hole where the object is, for compositing over a plate. |
| `shadowCatcher` | `boolean?` | — | `null` | Render only the shadows and reflections the object receives (Cycles). |
| `visible` | `object?` | — | `null` | Ray visibility per ray type. |
| `passIndex` | `integer?` | — | `null` | Value written to the IndexOB pass; an ID Mask node in blender_compositor isolates it. |
| `lightGroup` | `string?` | — | `null` | Light group for lights and emissive meshes; empty string removes it. |
| `caustics` | `object?` | — | `null` | Caustics role. |
| `motionBlur` | `object?` | — | `null` | Per-object Cycles motion blur. |
| `shadowTerminatorOffset` | `number?` | — | `null` | Shadow terminator offset 0 to 1; hides the shading artefact on low-poly smooth meshes. |

<details><summary>JSON Schema</summary>

```json
{
  "type": "object",
  "properties": {
    "names": {
      "description": "Objects, or collection names to apply to every object inside.",
      "type": "array",
      "items": {
        "type": "string"
      }
    },
    "holdout": {
      "description": "Cut a transparent hole where the object is, for compositing over a plate.",
      "type": [
        "boolean",
        "null"
      ],
      "default": null
    },
    "shadowCatcher": {
      "description": "Render only the shadows and reflections the object receives (Cycles).",
      "type": [
        "boolean",
        "null"
      ],
      "default": null
    },
    "visible": {
      "description": "Ray visibility per ray type.",
      "type": [
        "object",
        "null"
      ],
      "properties": {
        "camera": {
          "type": [
            "boolean",
            "null"
          ]
        },
        "diffuse": {
          "type": [
            "boolean",
            "null"
          ]
        },
        "glossy": {
          "type": [
            "boolean",
            "null"
          ]
        },
        "transmission": {
          "type": [
            "boolean",
            "null"
          ]
        },
        "volumeScatter": {
          "type": [
            "boolean",
            "null"
          ]
        },
        "shadow": {
          "type": [
            "boolean",
            "null"
          ]
        }
      },
      "default": null
    },
    "passIndex": {
      "description": "Value written to the IndexOB pass; an ID Mask node in blender_compositor isolates it.",
      "type": [
        "integer",
        "null"
      ],
      "default": null
    },
    "lightGroup": {
      "description": "Light group for lights and emissive meshes; empty string removes it.",
      "type": [
        "string",
        "null"
      ],
      "default": null
    },
    "caustics": {
      "description": "Caustics role.",
      "type": [
        "object",
        "null"
      ],
      "properties": {
        "caster": {
          "type": [
            "boolean",
            "null"
          ]
        },
        "receiver": {
          "type": [
            "boolean",
            "null"
          ]
        }
      },
      "default": null
    },
    "motionBlur": {
      "description": "Per-object Cycles motion blur.",
      "type": [
        "object",
        "null"
      ],
      "properties": {
        "enabled": {
          "description": "Blur this object\u0027s motion.",
          "type": [
            "boolean",
            "null"
          ]
        },
        "steps": {
          "description": "Motion steps 1 to 7; more steps blur curved motion correctly.",
          "type": [
            "integer",
            "null"
          ]
        },
        "deform": {
          "description": "Blur deformation as well as transforms.",
          "type": [
            "boolean",
            "null"
          ]
        }
      },
      "default": null
    },
    "shadowTerminatorOffset": {
      "description": "Shadow terminator offset 0 to 1; hides the shading artefact on low-poly smooth meshes.",
      "type": [
        "number",
        "null"
      ],
      "default": null
    }
  },
  "required": [
    "names"
  ]
}
```

</details>

