# Colour management and output

Part of the `rendering` skill: these tools appear once it is loaded — by `blender_enable_skill` with `rendering`, or by `find_tool` when a search returns one of them.

## <code>blender_<wbr>set_<wbr>color_<wbr>management</code>

Sets colour management: display device, view transform (AgX, Filmic, Standard, ACES, Khronos PBR Neutral), look, exposure, gamma, white balance and the sequencer colour space. Returns the effective values and what this Blender offers.

*only adds · every call adds · stays inside Blender.*

| Parameter | Type | Required | Default | Description |
| --- | --- | --- | --- | --- |
| `displayDevice` | `string?` | — | `null` | sRGB (the default), Display P3, Rec.1886, Rec.2020, Rec.2100-PQ, Rec.2100-HLG. |
| `viewTransform` | `string?` | — | `null` | AgX (the default, filmic highlights), Filmic, Standard (no tone mapping), Khronos PBR Neutral, ACES 1.3, ACES 2.0, False Color, Raw. |
| `look` | `string?` | — | `null` | Look of the view transform: None, Punchy, Greyscale, Very High Contrast, High Contrast, Medium High Contrast, Base Contrast, Medium Low Contrast, Low Contrast, Very Low Contrast; the AgX prefix is added when needed. |
| `exposure` | `number?` | — | `null` | Exposure in stops; 0 is neutral. |
| `gamma` | `number?` | — | `null` | Display gamma; 1 is neutral. |
| `useCurves` | `boolean?` | — | `null` | Apply the RGB curves of the colour management panel. |
| `whiteBalance` | `object?` | — | `null` | White balance of the view. |
| `sequencerColorspace` | `string?` | — | `null` | Colour space of sequencer strips, e.g. sRGB, ACEScg, AgX Log, Linear Rec.709. |
| `scene` | `string?` | — | `null` | Scene to act on; the current scene otherwise. |

<details><summary>JSON Schema</summary>

```json
{
  "type": "object",
  "properties": {
    "displayDevice": {
      "description": "sRGB (the default), Display P3, Rec.1886, Rec.2020, Rec.2100-PQ, Rec.2100-HLG.",
      "type": [
        "string",
        "null"
      ],
      "default": null
    },
    "viewTransform": {
      "description": "AgX (the default, filmic highlights), Filmic, Standard (no tone mapping), Khronos PBR Neutral, ACES 1.3, ACES 2.0, False Color, Raw.",
      "type": [
        "string",
        "null"
      ],
      "default": null
    },
    "look": {
      "description": "Look of the view transform: None, Punchy, Greyscale, Very High Contrast, High Contrast, Medium High Contrast, Base Contrast, Medium Low Contrast, Low Contrast, Very Low Contrast; the AgX prefix is added when needed.",
      "type": [
        "string",
        "null"
      ],
      "default": null
    },
    "exposure": {
      "description": "Exposure in stops; 0 is neutral.",
      "type": [
        "number",
        "null"
      ],
      "default": null
    },
    "gamma": {
      "description": "Display gamma; 1 is neutral.",
      "type": [
        "number",
        "null"
      ],
      "default": null
    },
    "useCurves": {
      "description": "Apply the RGB curves of the colour management panel.",
      "type": [
        "boolean",
        "null"
      ],
      "default": null
    },
    "whiteBalance": {
      "description": "White balance of the view.",
      "type": [
        "object",
        "null"
      ],
      "properties": {
        "enabled": {
          "description": "On or off; on when temperature or tint is given.",
          "type": [
            "boolean",
            "null"
          ]
        },
        "temperature": {
          "description": "Kelvin; 6500 is neutral daylight, lower warms.",
          "type": [
            "number",
            "null"
          ]
        },
        "tint": {
          "description": "Green to magenta shift.",
          "type": [
            "number",
            "null"
          ]
        }
      },
      "default": null
    },
    "sequencerColorspace": {
      "description": "Colour space of sequencer strips, e.g. sRGB, ACEScg, AgX Log, Linear Rec.709.",
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
  }
}
```

</details>

## <code>blender_<wbr>set_<wbr>output</code>

Sets the render output: path template with #### for the frame number, format, colour depth, EXR codec, PNG compression, JPEG quality, overwrite and placeholder for farm-style rendering, metadata stamps, a render region, a resolution preset and stereo or multi-view. Returns the effective settings and the first file name Blender would write.

*only adds · every call adds · stays inside Blender.*

| Parameter | Type | Required | Default | Description |
| --- | --- | --- | --- | --- |
| `path` | `string?` | — | `null` | Output path template: a folder plus a prefix, #### where the frame number goes, e.g. /renders/shot01/beauty_####. |
| `fileFormat` | `string?` | — | `null` | PNG, JPEG, OPEN_EXR, OPEN_EXR_MULTILAYER, TIFF, WEBP, BMP, DPX, CINEON, TARGA, HDR, JPEG2000, AVIF, or FFMPEG for video. |
| `colorMode` | `string?` | — | `null` | BW, RGB or RGBA. |
| `colorDepth` | `integer?` | — | `null` | Bits per channel: 8 or 16 for PNG and TIFF, 16 (half) or 32 (full float) for EXR, 8, 10 or 12 for video and DPX. |
| `exrCodec` | `string?` | — | `null` | EXR compression: ZIP (lossless, the default), PIZ, ZIPS, RLE, PXR24 (lossy for 32-bit), DWAA and DWAB (lossy, small), B44, B44A, HTJ2K or NONE. |
| `compression` | `integer?` | — | `null` | PNG compression 0 to 100; 15 is the default, higher is smaller and slower. |
| `quality` | `integer?` | — | `null` | JPEG, WebP and AVIF quality 0 to 100. |
| `overwrite` | `boolean?` | — | `null` | Overwrite frames that exist; false skips them, the way farms share a frame range. |
| `placeholder` | `boolean?` | — | `null` | Write an empty placeholder before rendering each frame so other machines skip it. |
| `fileExtension` | `boolean?` | — | `null` | Append the format's extension to the file name. |
| `colorManagement` | `string?` | — | `null` | FOLLOW_SCENE applies the view transform to the file, OVERRIDE keeps the file's own (used for linear EXR). |
| `stamp` | `object?` | — | `null` | Metadata burnt into the image. |
| `region` | `array?` | — | `null` | Render region [minX, minY, maxX, maxY] as fractions of the frame, 0 to 1; pass null to render the whole frame. |
| `cropToRegion` | `boolean?` | — | `null` | Crop the output to the region instead of leaving the rest empty. |
| `preset` | `string?` | — | `null` | Resolution preset, which also sets the percentage to 100: HD, FHD, QHD, UHD, 8K, DCI_2K, DCI_4K, SQUARE_1K, SQUARE_2K, PORTRAIT_FHD, PORTRAIT_UHD, CINEMASCOPE_2K, CINEMASCOPE_4K. |
| `stereo` | `object?` | — | `null` | Stereoscopic or multi-view rendering. |
| `scene` | `string?` | — | `null` | Scene to act on; the current scene otherwise. |

<details><summary>JSON Schema</summary>

```json
{
  "type": "object",
  "properties": {
    "path": {
      "description": "Output path template: a folder plus a prefix, #### where the frame number goes, e.g. /renders/shot01/beauty_####.",
      "type": [
        "string",
        "null"
      ],
      "default": null
    },
    "fileFormat": {
      "description": "PNG, JPEG, OPEN_EXR, OPEN_EXR_MULTILAYER, TIFF, WEBP, BMP, DPX, CINEON, TARGA, HDR, JPEG2000, AVIF, or FFMPEG for video.",
      "type": [
        "string",
        "null"
      ],
      "default": null
    },
    "colorMode": {
      "description": "BW, RGB or RGBA.",
      "type": [
        "string",
        "null"
      ],
      "default": null
    },
    "colorDepth": {
      "description": "Bits per channel: 8 or 16 for PNG and TIFF, 16 (half) or 32 (full float) for EXR, 8, 10 or 12 for video and DPX.",
      "type": [
        "integer",
        "null"
      ],
      "default": null
    },
    "exrCodec": {
      "description": "EXR compression: ZIP (lossless, the default), PIZ, ZIPS, RLE, PXR24 (lossy for 32-bit), DWAA and DWAB (lossy, small), B44, B44A, HTJ2K or NONE.",
      "type": [
        "string",
        "null"
      ],
      "default": null
    },
    "compression": {
      "description": "PNG compression 0 to 100; 15 is the default, higher is smaller and slower.",
      "type": [
        "integer",
        "null"
      ],
      "default": null
    },
    "quality": {
      "description": "JPEG, WebP and AVIF quality 0 to 100.",
      "type": [
        "integer",
        "null"
      ],
      "default": null
    },
    "overwrite": {
      "description": "Overwrite frames that exist; false skips them, the way farms share a frame range.",
      "type": [
        "boolean",
        "null"
      ],
      "default": null
    },
    "placeholder": {
      "description": "Write an empty placeholder before rendering each frame so other machines skip it.",
      "type": [
        "boolean",
        "null"
      ],
      "default": null
    },
    "fileExtension": {
      "description": "Append the format\u0027s extension to the file name.",
      "type": [
        "boolean",
        "null"
      ],
      "default": null
    },
    "colorManagement": {
      "description": "FOLLOW_SCENE applies the view transform to the file, OVERRIDE keeps the file\u0027s own (used for linear EXR).",
      "type": [
        "string",
        "null"
      ],
      "default": null
    },
    "stamp": {
      "description": "Metadata burnt into the image.",
      "type": [
        "object",
        "null"
      ],
      "properties": {
        "enabled": {
          "description": "Stamps on or off.",
          "type": [
            "boolean",
            "null"
          ]
        },
        "note": {
          "description": "Free text, e.g. the version; switches the note field on.",
          "type": [
            "string",
            "null"
          ]
        },
        "fontSize": {
          "type": [
            "integer",
            "null"
          ]
        },
        "time": {
          "type": [
            "boolean",
            "null"
          ]
        },
        "date": {
          "type": [
            "boolean",
            "null"
          ]
        },
        "frame": {
          "type": [
            "boolean",
            "null"
          ]
        },
        "frameRange": {
          "type": [
            "boolean",
            "null"
          ]
        },
        "camera": {
          "type": [
            "boolean",
            "null"
          ]
        },
        "lens": {
          "type": [
            "boolean",
            "null"
          ]
        },
        "scene": {
          "type": [
            "boolean",
            "null"
          ]
        },
        "marker": {
          "type": [
            "boolean",
            "null"
          ]
        },
        "filename": {
          "type": [
            "boolean",
            "null"
          ]
        },
        "renderTime": {
          "type": [
            "boolean",
            "null"
          ]
        },
        "memory": {
          "type": [
            "boolean",
            "null"
          ]
        },
        "hostname": {
          "type": [
            "boolean",
            "null"
          ]
        }
      },
      "default": null
    },
    "region": {
      "description": "Render region [minX, minY, maxX, maxY] as fractions of the frame, 0 to 1; pass null to render the whole frame.",
      "type": [
        "array",
        "null"
      ],
      "items": {
        "type": "number"
      },
      "default": null
    },
    "cropToRegion": {
      "description": "Crop the output to the region instead of leaving the rest empty.",
      "type": [
        "boolean",
        "null"
      ],
      "default": null
    },
    "preset": {
      "description": "Resolution preset, which also sets the percentage to 100: HD, FHD, QHD, UHD, 8K, DCI_2K, DCI_4K, SQUARE_1K, SQUARE_2K, PORTRAIT_FHD, PORTRAIT_UHD, CINEMASCOPE_2K, CINEMASCOPE_4K.",
      "type": [
        "string",
        "null"
      ],
      "default": null
    },
    "stereo": {
      "description": "Stereoscopic or multi-view rendering.",
      "type": [
        "object",
        "null"
      ],
      "properties": {
        "enabled": {
          "description": "Multi-view rendering on or off.",
          "type": [
            "boolean",
            "null"
          ]
        },
        "mode": {
          "description": "STEREO_3D (left and right eye) or MULTIVIEW (any number of cameras).",
          "type": [
            "string",
            "null"
          ]
        },
        "format": {
          "description": "File layout: INDIVIDUAL files per view or STEREO_3D packed (anaglyph, interlace, side by side).",
          "type": [
            "string",
            "null"
          ]
        },
        "convergenceMode": {
          "description": "OFFAXIS (the default), PARALLEL or TOE_IN.",
          "type": [
            "string",
            "null"
          ]
        },
        "convergenceDistance": {
          "description": "Distance in metres where the eyes converge.",
          "type": [
            "number",
            "null"
          ]
        },
        "interocularDistance": {
          "description": "Distance between the eyes in metres; 0.065 is human.",
          "type": [
            "number",
            "null"
          ]
        },
        "pivot": {
          "description": "CENTER, LEFT or RIGHT: which eye the camera object stands for.",
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

