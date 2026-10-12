# Video editing

Part of the `video` skill: these tools appear once it is loaded — by `blender_enable_skill` with `video`, or by `find_tool` when a search returns one of them.

## <code>blender_<wbr>sequencer_<wbr>info</code>

The timeline: every strip with its channel, frames, trims, blend and source, plus the frame range, fps, resolution and output format.

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

## <code>blender_<wbr>sequencer_<wbr>add_<wbr>strip</code>

Adds a strip to the timeline: a movie (with its sound), an image or an image sequence from a folder or file list, a sound file, a 3D scene, a solid colour or a text title, placed on a channel from a frame, with optional fades, transform and blend.

*only adds · every call adds · stays inside Blender.*

| Parameter | Type | Required | Default | Description |
| --- | --- | --- | --- | --- |
| `type` | `string` | yes | — | movie, image, sound, scene (renders a 3D scene into the timeline), color or text. |
| `name` | `string?` | — | `null` | Name for the strip. |
| `path` | `string?` | — | `null` | movie, image, sound: absolute path of the file. |
| `scene` | `string?` | — | `null` | scene: name of the scene to render into the strip; the current scene otherwise. |
| `channel` | `integer?` | — | `null` | Timeline channel (row), 1 and up; the first free one otherwise. |
| `frameStart` | `integer?` | — | `null` | Frame the strip starts on; the scene start otherwise. |
| `length` | `integer?` | — | `null` | color, text, image: length in frames. |
| `text` | `string?` | — | `null` | text: the text to show. |
| `fontSize` | `number?` | — | `null` | text: font size in pixels. |
| `color` | `array?` | — | `null` | color: [r, g, b]; text: [r, g, b] or [r, g, b, a]. |
| `location` | `array?` | — | `null` | text: [x, y] position, 0 to 1 from the bottom-left. |
| `withSound` | `boolean` | — | `true` | movie: also add its audio as a sound strip on the next channel. |
| `blendType` | `string?` | — | `null` | How the strip mixes with the channels below: REPLACE, ALPHA_OVER (the default for effects), CROSS, ADD, MULTIPLY, SCREEN, OVERLAY, DIFFERENCE and the other blend modes. |
| `opacity` | `number?` | — | `null` | Opacity 0 to 1. |
| `transform` | `object?` | — | `null` | Picture placement: offset, scale, rotation, crop and flip. |
| `fade` | `object?` | — | `null` | Fades in frames; opacity for pictures, volume for sound. |
| `settings` | `object?` | — | `null` | Further strip properties by Python name, e.g. text {"use_box": true, "wrap_width": 0.8}, SPEED {"speed_factor": 2}, WIPE {"transition_type": "CLOCK"}, GAUSSIAN_BLUR {"size_x": 20, "size_y": 20}, GLOW {"threshold": 0.5}. |

<details><summary>JSON Schema</summary>

```json
{
  "type": "object",
  "properties": {
    "type": {
      "description": "movie, image, sound, scene (renders a 3D scene into the timeline), color or text.",
      "type": "string"
    },
    "name": {
      "description": "Name for the strip.",
      "type": [
        "string",
        "null"
      ],
      "default": null
    },
    "path": {
      "description": "movie, image, sound: absolute path of the file.",
      "type": [
        "string",
        "null"
      ],
      "default": null
    },
    "scene": {
      "description": "scene: name of the scene to render into the strip; the current scene otherwise.",
      "type": [
        "string",
        "null"
      ],
      "default": null
    },
    "channel": {
      "description": "Timeline channel (row), 1 and up; the first free one otherwise.",
      "type": [
        "integer",
        "null"
      ],
      "default": null
    },
    "frameStart": {
      "description": "Frame the strip starts on; the scene start otherwise.",
      "type": [
        "integer",
        "null"
      ],
      "default": null
    },
    "length": {
      "description": "color, text, image: length in frames.",
      "type": [
        "integer",
        "null"
      ],
      "default": null
    },
    "text": {
      "description": "text: the text to show.",
      "type": [
        "string",
        "null"
      ],
      "default": null
    },
    "fontSize": {
      "description": "text: font size in pixels.",
      "type": [
        "number",
        "null"
      ],
      "default": null
    },
    "color": {
      "description": "color: [r, g, b]; text: [r, g, b] or [r, g, b, a].",
      "type": [
        "array",
        "null"
      ],
      "items": {
        "type": "number"
      },
      "default": null
    },
    "location": {
      "description": "text: [x, y] position, 0 to 1 from the bottom-left.",
      "type": [
        "array",
        "null"
      ],
      "items": {
        "type": "number"
      },
      "default": null
    },
    "withSound": {
      "description": "movie: also add its audio as a sound strip on the next channel.",
      "type": "boolean",
      "default": true
    },
    "blendType": {
      "description": "How the strip mixes with the channels below: REPLACE, ALPHA_OVER (the default for effects), CROSS, ADD, MULTIPLY, SCREEN, OVERLAY, DIFFERENCE and the other blend modes.",
      "type": [
        "string",
        "null"
      ],
      "default": null
    },
    "opacity": {
      "description": "Opacity 0 to 1.",
      "type": [
        "number",
        "null"
      ],
      "default": null
    },
    "transform": {
      "description": "Picture placement: offset, scale, rotation, crop and flip.",
      "type": [
        "object",
        "null"
      ],
      "properties": {
        "offset": {
          "description": "[x, y] offset in pixels.",
          "type": [
            "array",
            "null"
          ],
          "items": {
            "type": "number"
          }
        },
        "scale": {
          "description": "[sx, sy] scale factors.",
          "type": [
            "array",
            "null"
          ],
          "items": {
            "type": "number"
          }
        },
        "rotation": {
          "description": "Rotation in degrees.",
          "type": [
            "number",
            "null"
          ]
        },
        "crop": {
          "description": "[left, right, bottom, top] crop in pixels.",
          "type": [
            "array",
            "null"
          ],
          "items": {
            "type": "integer"
          }
        },
        "flip": {
          "description": "[x, y] mirror flags.",
          "type": [
            "array",
            "null"
          ],
          "items": {
            "type": "boolean"
          }
        }
      },
      "default": null
    },
    "fade": {
      "description": "Fades in frames; opacity for pictures, volume for sound.",
      "type": [
        "object",
        "null"
      ],
      "properties": {
        "in": {
          "description": "Frames of fade-in.",
          "type": [
            "integer",
            "null"
          ]
        },
        "out": {
          "description": "Frames of fade-out.",
          "type": [
            "integer",
            "null"
          ]
        }
      },
      "default": null
    },
    "settings": {
      "description": "Further strip properties by Python name, e.g. text {\u0022use_box\u0022: true, \u0022wrap_width\u0022: 0.8}, SPEED {\u0022speed_factor\u0022: 2}, WIPE {\u0022transition_type\u0022: \u0022CLOCK\u0022}, GAUSSIAN_BLUR {\u0022size_x\u0022: 20, \u0022size_y\u0022: 20}, GLOW {\u0022threshold\u0022: 0.5}.",
      "type": [
        "object",
        "null"
      ],
      "default": null
    }
  },
  "required": [
    "type"
  ]
}
```

</details>

## <code>blender_<wbr>sequencer_<wbr>add_<wbr>effect</code>

Adds a transition or effect strip: a cross-fade or wipe between two strips where they overlap, a blur, glow or speed change on one, or a generated colour or text.

*only adds · every call adds · stays inside Blender.*

| Parameter | Type | Required | Default | Description |
| --- | --- | --- | --- | --- |
| `type` | `string` | yes | — | Transitions between two strips: CROSS, GAMMA_CROSS, WIPE; mixes of two: ADD, SUBTRACT, MULTIPLY, ALPHA_OVER, ALPHA_UNDER, COLORMIX; on one strip: GLOW, GAUSSIAN_BLUR, SPEED, ADJUSTMENT; generators: COLOR, TEXT; special: MULTICAM, COMPOSITOR. |
| `inputs` | `array?` | — | `null` | Input strip names: two for transitions and mixes, one for GLOW, GAUSSIAN_BLUR, SPEED and ADJUSTMENT, none for COLOR and TEXT. |
| `name` | `string?` | — | `null` | Name for the effect strip. |
| `channel` | `integer?` | — | `null` | Timeline channel; the first free one otherwise. |
| `frameStart` | `integer?` | — | `null` | Start frame; where the inputs overlap otherwise. |
| `length` | `integer?` | — | `null` | Length in frames; the overlap of the inputs otherwise. |
| `settings` | `object?` | — | `null` | Further strip properties by Python name, e.g. text {"use_box": true, "wrap_width": 0.8}, SPEED {"speed_factor": 2}, WIPE {"transition_type": "CLOCK"}, GAUSSIAN_BLUR {"size_x": 20, "size_y": 20}, GLOW {"threshold": 0.5}. |

<details><summary>JSON Schema</summary>

```json
{
  "type": "object",
  "properties": {
    "type": {
      "description": "Transitions between two strips: CROSS, GAMMA_CROSS, WIPE; mixes of two: ADD, SUBTRACT, MULTIPLY, ALPHA_OVER, ALPHA_UNDER, COLORMIX; on one strip: GLOW, GAUSSIAN_BLUR, SPEED, ADJUSTMENT; generators: COLOR, TEXT; special: MULTICAM, COMPOSITOR.",
      "type": "string"
    },
    "inputs": {
      "description": "Input strip names: two for transitions and mixes, one for GLOW, GAUSSIAN_BLUR, SPEED and ADJUSTMENT, none for COLOR and TEXT.",
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
    "name": {
      "description": "Name for the effect strip.",
      "type": [
        "string",
        "null"
      ],
      "default": null
    },
    "channel": {
      "description": "Timeline channel; the first free one otherwise.",
      "type": [
        "integer",
        "null"
      ],
      "default": null
    },
    "frameStart": {
      "description": "Start frame; where the inputs overlap otherwise.",
      "type": [
        "integer",
        "null"
      ],
      "default": null
    },
    "length": {
      "description": "Length in frames; the overlap of the inputs otherwise.",
      "type": [
        "integer",
        "null"
      ],
      "default": null
    },
    "settings": {
      "description": "Further strip properties by Python name, e.g. text {\u0022use_box\u0022: true, \u0022wrap_width\u0022: 0.8}, SPEED {\u0022speed_factor\u0022: 2}, WIPE {\u0022transition_type\u0022: \u0022CLOCK\u0022}, GAUSSIAN_BLUR {\u0022size_x\u0022: 20, \u0022size_y\u0022: 20}, GLOW {\u0022threshold\u0022: 0.5}.",
      "type": [
        "object",
        "null"
      ],
      "default": null
    }
  },
  "required": [
    "type"
  ]
}
```

</details>

## <code>blender_<wbr>sequencer_<wbr>update_<wbr>strip</code>

Moves, trims, mutes, fades, re-blends, transforms or renames a strip; changes text, colour or volume.

*only adds · every call adds · stays inside Blender.*

| Parameter | Type | Required | Default | Description |
| --- | --- | --- | --- | --- |
| `name` | `string` | yes | — | Strip name as blender_sequencer_info lists it. |
| `channel` | `integer?` | — | `null` | Move to this channel. |
| `frameStart` | `integer?` | — | `null` | Move the strip to start on this frame. |
| `length` | `integer?` | — | `null` | New length in frames (for images, colours, text and effects). |
| `trimStart` | `integer?` | — | `null` | Frames cut from the beginning of the source. |
| `trimEnd` | `integer?` | — | `null` | Frames cut from the end of the source. |
| `mute` | `boolean?` | — | `null` | Silence or hide the strip. |
| `blendType` | `string?` | — | `null` | How the strip mixes with the channels below: REPLACE, ALPHA_OVER (the default for effects), CROSS, ADD, MULTIPLY, SCREEN, OVERLAY, DIFFERENCE and the other blend modes. |
| `opacity` | `number?` | — | `null` | Opacity 0 to 1. |
| `volume` | `number?` | — | `null` | sound: volume, 1 is the source level. |
| `text` | `string?` | — | `null` | text: new text. |
| `color` | `array?` | — | `null` | color: new [r, g, b]. |
| `transform` | `object?` | — | `null` | Picture placement: offset, scale, rotation, crop and flip. |
| `fade` | `object?` | — | `null` | Fades in frames; opacity for pictures, volume for sound. |
| `settings` | `object?` | — | `null` | Further strip properties by Python name, e.g. text {"use_box": true, "wrap_width": 0.8}, SPEED {"speed_factor": 2}, WIPE {"transition_type": "CLOCK"}, GAUSSIAN_BLUR {"size_x": 20, "size_y": 20}, GLOW {"threshold": 0.5}. |
| `newName` | `string?` | — | `null` | Rename the strip. |

<details><summary>JSON Schema</summary>

```json
{
  "type": "object",
  "properties": {
    "name": {
      "description": "Strip name as blender_sequencer_info lists it.",
      "type": "string"
    },
    "channel": {
      "description": "Move to this channel.",
      "type": [
        "integer",
        "null"
      ],
      "default": null
    },
    "frameStart": {
      "description": "Move the strip to start on this frame.",
      "type": [
        "integer",
        "null"
      ],
      "default": null
    },
    "length": {
      "description": "New length in frames (for images, colours, text and effects).",
      "type": [
        "integer",
        "null"
      ],
      "default": null
    },
    "trimStart": {
      "description": "Frames cut from the beginning of the source.",
      "type": [
        "integer",
        "null"
      ],
      "default": null
    },
    "trimEnd": {
      "description": "Frames cut from the end of the source.",
      "type": [
        "integer",
        "null"
      ],
      "default": null
    },
    "mute": {
      "description": "Silence or hide the strip.",
      "type": [
        "boolean",
        "null"
      ],
      "default": null
    },
    "blendType": {
      "description": "How the strip mixes with the channels below: REPLACE, ALPHA_OVER (the default for effects), CROSS, ADD, MULTIPLY, SCREEN, OVERLAY, DIFFERENCE and the other blend modes.",
      "type": [
        "string",
        "null"
      ],
      "default": null
    },
    "opacity": {
      "description": "Opacity 0 to 1.",
      "type": [
        "number",
        "null"
      ],
      "default": null
    },
    "volume": {
      "description": "sound: volume, 1 is the source level.",
      "type": [
        "number",
        "null"
      ],
      "default": null
    },
    "text": {
      "description": "text: new text.",
      "type": [
        "string",
        "null"
      ],
      "default": null
    },
    "color": {
      "description": "color: new [r, g, b].",
      "type": [
        "array",
        "null"
      ],
      "items": {
        "type": "number"
      },
      "default": null
    },
    "transform": {
      "description": "Picture placement: offset, scale, rotation, crop and flip.",
      "type": [
        "object",
        "null"
      ],
      "properties": {
        "offset": {
          "description": "[x, y] offset in pixels.",
          "type": [
            "array",
            "null"
          ],
          "items": {
            "type": "number"
          }
        },
        "scale": {
          "description": "[sx, sy] scale factors.",
          "type": [
            "array",
            "null"
          ],
          "items": {
            "type": "number"
          }
        },
        "rotation": {
          "description": "Rotation in degrees.",
          "type": [
            "number",
            "null"
          ]
        },
        "crop": {
          "description": "[left, right, bottom, top] crop in pixels.",
          "type": [
            "array",
            "null"
          ],
          "items": {
            "type": "integer"
          }
        },
        "flip": {
          "description": "[x, y] mirror flags.",
          "type": [
            "array",
            "null"
          ],
          "items": {
            "type": "boolean"
          }
        }
      },
      "default": null
    },
    "fade": {
      "description": "Fades in frames; opacity for pictures, volume for sound.",
      "type": [
        "object",
        "null"
      ],
      "properties": {
        "in": {
          "description": "Frames of fade-in.",
          "type": [
            "integer",
            "null"
          ]
        },
        "out": {
          "description": "Frames of fade-out.",
          "type": [
            "integer",
            "null"
          ]
        }
      },
      "default": null
    },
    "settings": {
      "description": "Further strip properties by Python name, e.g. text {\u0022use_box\u0022: true, \u0022wrap_width\u0022: 0.8}, SPEED {\u0022speed_factor\u0022: 2}, WIPE {\u0022transition_type\u0022: \u0022CLOCK\u0022}, GAUSSIAN_BLUR {\u0022size_x\u0022: 20, \u0022size_y\u0022: 20}, GLOW {\u0022threshold\u0022: 0.5}.",
      "type": [
        "object",
        "null"
      ],
      "default": null
    },
    "newName": {
      "description": "Rename the strip.",
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

## <code>blender_<wbr>sequencer_<wbr>remove_<wbr>strip</code>

Removes one strip, or every strip with all=true.

*may overwrite or delete · every call adds · stays inside Blender.*

| Parameter | Type | Required | Default | Description |
| --- | --- | --- | --- | --- |
| `name` | `string?` | — | `null` | Strip to remove. |
| `all` | `boolean` | — | `false` | Remove every strip and start over. |

<details><summary>JSON Schema</summary>

```json
{
  "type": "object",
  "properties": {
    "name": {
      "description": "Strip to remove.",
      "type": [
        "string",
        "null"
      ],
      "default": null
    },
    "all": {
      "description": "Remove every strip and start over.",
      "type": "boolean",
      "default": false
    }
  }
}
```

</details>

## <code>blender_<wbr>sequencer_<wbr>render</code>

Encodes the timeline to a video file with FFmpeg: container by extension, codec, quality and audio codec, or whatever blender_sequencer_encode prepared. The frame range is fitted to the strips unless given. Long for big timelines: set a timeout that fits.

*only adds · every call adds · stays inside Blender.*

| Parameter | Type | Required | Default | Description |
| --- | --- | --- | --- | --- |
| `path` | `string` | yes | — | Absolute path of the video to write; the extension picks the container: .mp4, .mkv, .webm, .mov, .avi, .ogv. |
| `codec` | `string?` | — | `null` | H264 (the default), H265, AV1, WEBM (VP9), DNXHD, FFV1 and the rest FFmpeg offers. |
| `quality` | `string?` | — | `null` | LOSSLESS, PERC_LOSSLESS, HIGH, MEDIUM (the default), LOW, VERYLOW, LOWEST. |
| `audioCodec` | `string?` | — | `null` | AAC (the default), MP3, VORBIS, OPUS, FLAC, PCM or NONE. |
| `fitRange` | `boolean` | — | `true` | Set the frame range to cover every strip before encoding. |
| `start` | `integer?` | — | `null` | First frame to encode; overrides fitRange. |
| `end` | `integer?` | — | `null` | Last frame to encode; overrides fitRange. |
| `fps` | `integer?` | — | `null` | Frames per second. |
| `resolutionX` | `integer?` | — | `null` | Pixels along this edge, 1 to 4096. |
| `resolutionY` | `integer?` | — | `null` | Pixels along this edge, 1 to 4096. |
| `timeoutSeconds` | `integer` | — | `600` | Seconds to wait for Blender, 1 to 600. |

<details><summary>JSON Schema</summary>

```json
{
  "type": "object",
  "properties": {
    "path": {
      "description": "Absolute path of the video to write; the extension picks the container: .mp4, .mkv, .webm, .mov, .avi, .ogv.",
      "type": "string"
    },
    "codec": {
      "description": "H264 (the default), H265, AV1, WEBM (VP9), DNXHD, FFV1 and the rest FFmpeg offers.",
      "type": [
        "string",
        "null"
      ],
      "default": null
    },
    "quality": {
      "description": "LOSSLESS, PERC_LOSSLESS, HIGH, MEDIUM (the default), LOW, VERYLOW, LOWEST.",
      "type": [
        "string",
        "null"
      ],
      "default": null
    },
    "audioCodec": {
      "description": "AAC (the default), MP3, VORBIS, OPUS, FLAC, PCM or NONE.",
      "type": [
        "string",
        "null"
      ],
      "default": null
    },
    "fitRange": {
      "description": "Set the frame range to cover every strip before encoding.",
      "type": "boolean",
      "default": true
    },
    "start": {
      "description": "First frame to encode; overrides fitRange.",
      "type": [
        "integer",
        "null"
      ],
      "default": null
    },
    "end": {
      "description": "Last frame to encode; overrides fitRange.",
      "type": [
        "integer",
        "null"
      ],
      "default": null
    },
    "fps": {
      "description": "Frames per second.",
      "type": [
        "integer",
        "null"
      ],
      "default": null
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
    "timeoutSeconds": {
      "description": "Seconds to wait for Blender, 1 to 600.",
      "type": "integer",
      "default": 600
    }
  },
  "required": [
    "path"
  ]
}
```

</details>

