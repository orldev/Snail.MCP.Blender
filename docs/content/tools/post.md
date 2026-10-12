# Lens effects

Part of the `post` skill: these tools appear once it is loaded — by `blender_enable_skill` with `post`, or by `find_tool` when a search returns one of them.

## <code>blender_<wbr>lens_<wbr>effects</code>

Rebuilds the post stack on the beauty pass in a compositor's order: denoise (with the denoising data passes), depth of field or motion blur from passes, glare, halation, chromatic aberration, vignette, film grain weighted by luminance and animated per frame, in display space. Each block optional, but at least one is asked for: a call naming none is refused rather than read as clear. A repeated call replaces the stack, clear removes it and wires Render Layers straight to the output. To see the chain that is there, read it with blender_compositor and action info. Data passes stay raw for blender_file_output. Returns the chain and warnings.

*only adds · every call adds · stays inside Blender.*

| Parameter | Type | Required | Default | Description |
| --- | --- | --- | --- | --- |
| `denoise` | `object?` | — | `null` | Denoise node at the head of the stack. |
| `defocus` | `object?` | — | `null` | Depth of field from the Z pass. |
| `vectorBlur` | `object?` | — | `null` | Motion blur from the Vector pass; scene motion blur must be off. |
| `glare` | `object?` | — | `null` | Glare: bloom, streaks, ghosts. |
| `halation` | `object?` | — | `null` | Halation, the red halo of film around highlights. |
| `chromaticAberration` | `object?` | — | `null` | Chromatic aberration and barrel distortion. |
| `vignette` | `object?` | — | `null` | Darkened corners. |
| `grain` | `object?` | — | `null` | Film grain, animated and weighted by luminance. |
| `clear` | `boolean` | — | `false` | Remove the stack and wire Render Layers straight to the output; the only way to take it down, since a call naming no effect is refused. |
| `scene` | `string?` | — | `null` | Scene to act on; the current scene otherwise. |

<details><summary>JSON Schema</summary>

```json
{
  "type": "object",
  "properties": {
    "denoise": {
      "description": "Denoise node at the head of the stack.",
      "type": [
        "object",
        "null"
      ],
      "properties": {
        "enabled": {
          "description": "Off removes the block; on when any other field is given.",
          "type": [
            "boolean",
            "null"
          ]
        },
        "useDataPasses": {
          "description": "Feed albedo and normal from the Cycles denoising data pass and switch Cycles\u0027 own denoising off; the studio way.",
          "type": [
            "boolean",
            "null"
          ]
        },
        "prefilter": {
          "description": "ACCURATE (the default), FAST or NONE.",
          "type": [
            "string",
            "null"
          ]
        },
        "quality": {
          "description": "FOLLOW_SCENE, HIGH, BALANCED or FAST.",
          "type": [
            "string",
            "null"
          ]
        },
        "hdr": {
          "description": "Treat the image as HDR; on by default.",
          "type": [
            "boolean",
            "null"
          ]
        }
      },
      "default": null
    },
    "defocus": {
      "description": "Depth of field from the Z pass.",
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
        "fstop": {
          "description": "f-number of the simulated lens; lower blurs more.",
          "type": [
            "number",
            "null"
          ]
        },
        "maxBlur": {
          "description": "Largest blur radius in pixels.",
          "type": [
            "number",
            "null"
          ]
        },
        "bokeh": {
          "description": "CIRCLE, TRIANGLE, SQUARE, PENTAGON, HEXAGON, HEPTAGON or OCTAGON.",
          "type": [
            "string",
            "null"
          ]
        },
        "rotation": {
          "description": "Bokeh rotation in degrees.",
          "type": [
            "number",
            "null"
          ]
        },
        "zScale": {
          "description": "Scale of the Z values; 1 for metres.",
          "type": [
            "number",
            "null"
          ]
        }
      },
      "default": null
    },
    "vectorBlur": {
      "description": "Motion blur from the Vector pass; scene motion blur must be off.",
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
        "samples": {
          "description": "Samples along the motion; 32 is smooth.",
          "type": [
            "integer",
            "null"
          ]
        },
        "shutter": {
          "description": "Shutter time in frames; 0.5 is a 180\u00B0 shutter.",
          "type": [
            "number",
            "null"
          ]
        }
      },
      "default": null
    },
    "glare": {
      "description": "Glare: bloom, streaks, ghosts.",
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
        "type": {
          "description": "BLOOM, GHOSTS, STREAKS, FOG_GLOW, SIMPLE_STAR or SUN_BEAMS.",
          "type": [
            "string",
            "null"
          ]
        },
        "threshold": {
          "description": "Scene-linear value above which pixels glow; 1 is display white.",
          "type": [
            "number",
            "null"
          ]
        },
        "smoothness": {
          "description": "Softness of the threshold.",
          "type": [
            "number",
            "null"
          ]
        },
        "strength": {
          "description": "Mix of the glare over the image, 0 to 1; 0.1 is subtle.",
          "type": [
            "number",
            "null"
          ]
        },
        "saturation": {
          "description": "Saturation of the glare; 1 keeps the source colour.",
          "type": [
            "number",
            "null"
          ]
        },
        "tint": {
          "description": "Tint [r, g, b].",
          "type": [
            "array",
            "null"
          ],
          "items": {
            "type": "number"
          }
        },
        "size": {
          "description": "Spread of the glare; bloom 4 to 9, streaks in pixels.",
          "type": [
            "number",
            "null"
          ]
        },
        "streaks": {
          "description": "Number of streaks.",
          "type": [
            "integer",
            "null"
          ]
        },
        "angle": {
          "description": "Streak angle offset in degrees.",
          "type": [
            "number",
            "null"
          ]
        },
        "iterations": {
          "description": "Iterations for streaks and ghosts.",
          "type": [
            "integer",
            "null"
          ]
        },
        "fade": {
          "description": "Fade of streaks along their length.",
          "type": [
            "number",
            "null"
          ]
        },
        "quality": {
          "description": "HIGH, MEDIUM or LOW.",
          "type": [
            "string",
            "null"
          ]
        }
      },
      "default": null
    },
    "halation": {
      "description": "Halation, the red halo of film around highlights.",
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
        "threshold": {
          "description": "Scene-linear value above which the halo forms.",
          "type": [
            "number",
            "null"
          ]
        },
        "size": {
          "description": "Halo radius; 6 is the default.",
          "type": [
            "number",
            "null"
          ]
        },
        "strength": {
          "description": "Mix of the halo, 0 to 1; 0.15 is film.",
          "type": [
            "number",
            "null"
          ]
        },
        "tint": {
          "description": "Halo colour [r, g, b]; warm red by default.",
          "type": [
            "array",
            "null"
          ],
          "items": {
            "type": "number"
          }
        }
      },
      "default": null
    },
    "chromaticAberration": {
      "description": "Chromatic aberration and barrel distortion.",
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
        "dispersion": {
          "description": "Colour fringing 0 to 1; 0.02 subtle, 0.1 a cheap lens.",
          "type": [
            "number",
            "null"
          ]
        },
        "distortion": {
          "description": "Barrel above 0, pincushion below.",
          "type": [
            "number",
            "null"
          ]
        },
        "jitter": {
          "description": "Jitter samples to hide banding.",
          "type": [
            "boolean",
            "null"
          ]
        },
        "fit": {
          "description": "Scale to keep the frame filled.",
          "type": [
            "boolean",
            "null"
          ]
        }
      },
      "default": null
    },
    "vignette": {
      "description": "Darkened corners.",
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
        "amount": {
          "description": "Darkening 0 to 1; 0.3 is gentle.",
          "type": [
            "number",
            "null"
          ]
        },
        "softness": {
          "description": "Edge softness 0 to 1.",
          "type": [
            "number",
            "null"
          ]
        },
        "roundness": {
          "description": "1 is an ellipse matching the frame, below 1 rounder.",
          "type": [
            "number",
            "null"
          ]
        }
      },
      "default": null
    },
    "grain": {
      "description": "Film grain, animated and weighted by luminance.",
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
        "strength": {
          "description": "Amount 0 to 1; 0.05 to 0.15 reads as film.",
          "type": [
            "number",
            "null"
          ]
        },
        "size": {
          "description": "Grain size in pixels at output resolution; 1.5 for 35 mm at 4K.",
          "type": [
            "number",
            "null"
          ]
        },
        "chroma": {
          "description": "0 monochrome grain, 1 full colour grain; 0.3 by default.",
          "type": [
            "number",
            "null"
          ]
        },
        "animated": {
          "description": "A new pattern every frame; on by default.",
          "type": [
            "boolean",
            "null"
          ]
        },
        "space": {
          "description": "DISPLAY adds the grain after the view transform so clipped highlights stay clean; LINEAR before it.",
          "type": [
            "string",
            "null"
          ]
        },
        "response": {
          "type": [
            "object",
            "null"
          ],
          "properties": {
            "shadows": {
              "description": "Weight in the shadows; 0.6 by default.",
              "type": [
                "number",
                "null"
              ]
            },
            "midtones": {
              "description": "Weight in the midtones; 1 by default.",
              "type": [
                "number",
                "null"
              ]
            },
            "highlights": {
              "description": "Weight in the highlights; 0.2 by default.",
              "type": [
                "number",
                "null"
              ]
            }
          }
        }
      },
      "default": null
    },
    "clear": {
      "description": "Remove the stack and wire Render Layers straight to the output; the only way to take it down, since a call naming no effect is refused.",
      "type": "boolean",
      "default": false
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

