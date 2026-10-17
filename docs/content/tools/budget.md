# Render budget

Part of the `farm` skill: these tools appear once it is loaded — by `blender_enable_skill` with `farm`, or by `find_tool` when a search returns one of them.

## <code>blender_<wbr>render_<wbr>budget</code>

What the frame will cost before rendering it: texture memory by size and depth, geometry after modifiers, instances, particles and hair, the Cycles devices and system memory, and a probe that renders the frame twice at low resolution — the pair separates the fixed cost of a frame from the cost that grows with its pixels, and the estimate is their sum for the whole frame. The memory estimate says where it came from: the probe's own peak plus the frame buffer it did not fill, or the textures and geometry when no probe ran. Verdict fits, tight or exceeds against a memory limit, with recommendations that can be applied in the same call: texture limit, simplify, tile size, device. Optionally packs external files and saves a copy for a farm.

*only adds · every call adds · stays inside Blender.*

| Parameter | Type | Required | Default | Description |
| --- | --- | --- | --- | --- |
| `probe` | `object?` | — | `null` | Probe render at low resolution to time the frame and measure peak memory; omit to skip. |
| `apply` | `object?` | — | `null` | Limits to apply now. |
| `packForFarm` | `object?` | — | `null` | Prepare the file for a farm. |
| `memoryLimitMb` | `integer?` | — | `null` | Memory the render may use in MB; 80 percent of what this machine, or the container Blender runs in, allows it otherwise. |
| `scene` | `string?` | — | `null` | Scene to act on; the current scene otherwise. |
| `timeoutSeconds` | `integer` | — | `300` | Seconds to wait for Blender, 1 to 600. |

<details><summary>JSON Schema</summary>

```json
{
  "type": "object",
  "properties": {
    "probe": {
      "description": "Probe render at low resolution to time the frame and measure peak memory; omit to skip.",
      "type": [
        "object",
        "null"
      ],
      "properties": {
        "enabled": {
          "description": "Off skips the probe; on when any other field is given.",
          "type": [
            "boolean",
            "null"
          ]
        },
        "percentage": {
          "description": "Resolution percentage of the probe, 1 to 50; 10 by default.",
          "type": [
            "integer",
            "null"
          ]
        },
        "samples": {
          "description": "Samples for the probe; the scene\u0027s otherwise.",
          "type": [
            "integer",
            "null"
          ]
        }
      },
      "default": null
    },
    "apply": {
      "description": "Limits to apply now.",
      "type": [
        "object",
        "null"
      ],
      "properties": {
        "textureLimit": {
          "description": "Largest texture edge at render time: OFF, 128, 256, 512, 1024, 2048, 4096 or 8192.",
          "type": [
            "string",
            "null"
          ]
        },
        "simplify": {
          "type": [
            "object",
            "null"
          ],
          "properties": {
            "enabled": {
              "type": [
                "boolean",
                "null"
              ]
            },
            "subdivision": {
              "description": "Maximum subdivision level at render time.",
              "type": [
                "integer",
                "null"
              ]
            },
            "childParticles": {
              "description": "Fraction of child particles rendered, 0 to 1.",
              "type": [
                "number",
                "null"
              ]
            },
            "volumes": {
              "description": "Volume resolution fraction, 0 to 1.",
              "type": [
                "number",
                "null"
              ]
            }
          }
        },
        "tileSize": {
          "description": "Cycles tile size in pixels; smaller lowers peak memory.",
          "type": [
            "integer",
            "null"
          ]
        },
        "device": {
          "description": "CPU, or a device name or type from the reply\u0027s devices list.",
          "type": [
            "string",
            "null"
          ]
        }
      },
      "default": null
    },
    "packForFarm": {
      "description": "Prepare the file for a farm.",
      "type": [
        "object",
        "null"
      ],
      "properties": {
        "enabled": {
          "type": [
            "boolean",
            "null"
          ]
        },
        "packExternal": {
          "description": "Pack external images and files into the .blend.",
          "type": [
            "boolean",
            "null"
          ]
        },
        "relativePaths": {
          "description": "Make every path relative to the file; needs a saved file.",
          "type": [
            "boolean",
            "null"
          ]
        },
        "copyTo": {
          "description": "Absolute path for a self-contained copy.",
          "type": [
            "string",
            "null"
          ]
        }
      },
      "default": null
    },
    "memoryLimitMb": {
      "description": "Memory the render may use in MB; 80 percent of what this machine, or the container Blender runs in, allows it otherwise.",
      "type": [
        "integer",
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
    },
    "timeoutSeconds": {
      "description": "Seconds to wait for Blender, 1 to 600.",
      "type": "integer",
      "default": 300
    }
  }
}
```

</details>

