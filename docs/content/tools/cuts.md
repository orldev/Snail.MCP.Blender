# Cuts, meta strips, proxies, timing and grading

Part of the `video` skill: these tools appear once it is loaded — by `blender_enable_skill` with `video`, or by `find_tool` when a search returns one of them.

## <code>blender_<wbr>sequencer_<wbr>split</code>

Cuts a strip at a frame into two strips on the same channel, keeping both or one side; movie, sound, image, scene, colour and text strips. Effects and transitions are cut by cutting their inputs.

*only adds · every call adds · stays inside Blender.*

| Parameter | Type | Required | Default | Description |
| --- | --- | --- | --- | --- |
| `name` | `string` | yes | — | Strip to cut, as blender_sequencer_info lists it. |
| `frame` | `integer` | yes | — | Frame where the cut goes; the right part starts on it. |
| `keep` | `string` | — | `both` | both (the default), left or right: which parts stay. |
| `rightName` | `string?` | — | `null` | Name for the right part; the strip's name plus .R otherwise. |

<details><summary>JSON Schema</summary>

```json
{
  "type": "object",
  "properties": {
    "name": {
      "description": "Strip to cut, as blender_sequencer_info lists it.",
      "type": "string"
    },
    "frame": {
      "description": "Frame where the cut goes; the right part starts on it.",
      "type": "integer"
    },
    "keep": {
      "description": "both (the default), left or right: which parts stay.",
      "type": "string",
      "default": "both"
    },
    "rightName": {
      "description": "Name for the right part; the strip\u0027s name plus .R otherwise.",
      "type": [
        "string",
        "null"
      ],
      "default": null
    }
  },
  "required": [
    "name",
    "frame"
  ]
}
```

</details>

## <code>blender_<wbr>sequencer_<wbr>proxy</code>

Sets up proxies (25, 50, 75, 100 percent) for smooth playback of heavy footage, per strip or for all, with optional project-level storage, and builds them when the Video Sequencer editor is open in Blender.

*only adds · every call adds · stays inside Blender.*

| Parameter | Type | Required | Default | Description |
| --- | --- | --- | --- | --- |
| `names` | `array?` | — | `null` | Strips to set up; every movie and image strip with all=true. |
| `all` | `boolean` | — | `false` | Every strip instead of a list. |
| `sizes` | `array?` | — | `null` | Proxy sizes in percent: 25, 50, 75, 100; [25] is the default and enough for editing. |
| `quality` | `integer?` | — | `null` | JPEG quality of the proxies, 1 to 100. |
| `directory` | `string?` | — | `null` | Project folder for the proxies instead of next to each source. |
| `overwrite` | `boolean` | — | `false` | Rebuild proxies that exist. |
| `build` | `boolean` | — | `true` | Build the proxies now; needs the Video Sequencer editor open in Blender, otherwise only the settings are stored. |
| `enabled` | `boolean` | — | `true` | Use proxies on these strips, or stop using them. |

<details><summary>JSON Schema</summary>

```json
{
  "type": "object",
  "properties": {
    "names": {
      "description": "Strips to set up; every movie and image strip with all=true.",
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
    "all": {
      "description": "Every strip instead of a list.",
      "type": "boolean",
      "default": false
    },
    "sizes": {
      "description": "Proxy sizes in percent: 25, 50, 75, 100; [25] is the default and enough for editing.",
      "type": [
        "array",
        "null"
      ],
      "items": {
        "type": "integer"
      },
      "default": null
    },
    "quality": {
      "description": "JPEG quality of the proxies, 1 to 100.",
      "type": [
        "integer",
        "null"
      ],
      "default": null
    },
    "directory": {
      "description": "Project folder for the proxies instead of next to each source.",
      "type": [
        "string",
        "null"
      ],
      "default": null
    },
    "overwrite": {
      "description": "Rebuild proxies that exist.",
      "type": "boolean",
      "default": false
    },
    "build": {
      "description": "Build the proxies now; needs the Video Sequencer editor open in Blender, otherwise only the settings are stored.",
      "type": "boolean",
      "default": true
    },
    "enabled": {
      "description": "Use proxies on these strips, or stop using them.",
      "type": "boolean",
      "default": true
    }
  }
}
```

</details>

## <code>blender_<wbr>sequencer_<wbr>timing</code>

Sets the project timing: frame rate including fractional rates (23.976, 29.97, 59.94), sync mode for playback (audio sync, frame drop), pixel aspect, resolution and the frame range.

*only adds · every call adds · stays inside Blender.*

| Parameter | Type | Required | Default | Description |
| --- | --- | --- | --- | --- |
| `fps` | `number?` | — | `null` | Frames per second; 23.976, 29.97 and 59.94 become the matching fractional rates. |
| `syncMode` | `string?` | — | `null` | NONE, FRAME_DROP (skip frames to keep time) or AUDIO_SYNC (follow the audio clock) for playback. |
| `pixelAspect` | `array?` | — | `null` | Pixel aspect [x, y]; [1, 1] for square pixels. |
| `resolutionX` | `integer?` | — | `null` | Pixels along this edge, 1 to 4096. |
| `resolutionY` | `integer?` | — | `null` | Pixels along this edge, 1 to 4096. |
| `percentage` | `integer?` | — | `null` | Resolution percentage 1 to 100. |
| `start` | `integer?` | — | `null` | First frame of the timeline. |
| `end` | `integer?` | — | `null` | Last frame of the timeline. |
| `audio` | `boolean?` | — | `null` | Play audio during playback. |

<details><summary>JSON Schema</summary>

```json
{
  "type": "object",
  "properties": {
    "fps": {
      "description": "Frames per second; 23.976, 29.97 and 59.94 become the matching fractional rates.",
      "type": [
        "number",
        "null"
      ],
      "default": null
    },
    "syncMode": {
      "description": "NONE, FRAME_DROP (skip frames to keep time) or AUDIO_SYNC (follow the audio clock) for playback.",
      "type": [
        "string",
        "null"
      ],
      "default": null
    },
    "pixelAspect": {
      "description": "Pixel aspect [x, y]; [1, 1] for square pixels.",
      "type": [
        "array",
        "null"
      ],
      "items": {
        "type": "number"
      },
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
    "percentage": {
      "description": "Resolution percentage 1 to 100.",
      "type": [
        "integer",
        "null"
      ],
      "default": null
    },
    "start": {
      "description": "First frame of the timeline.",
      "type": [
        "integer",
        "null"
      ],
      "default": null
    },
    "end": {
      "description": "Last frame of the timeline.",
      "type": [
        "integer",
        "null"
      ],
      "default": null
    },
    "audio": {
      "description": "Play audio during playback.",
      "type": [
        "boolean",
        "null"
      ],
      "default": null
    }
  }
}
```

</details>

## <code>blender_<wbr>sequencer_<wbr>grade</code>

Grades a strip with modifiers: colour balance (lift/gamma/gain or offset/power/slope), curves by channel, hue correct, brightness and contrast, white balance, tonemap, optionally masked by another strip; lists, updates or removes them.

*only adds · every call adds · stays inside Blender.*

| Parameter | Type | Required | Default | Description |
| --- | --- | --- | --- | --- |
| `name` | `string` | yes | — | Strip to grade. |
| `type` | `string?` | — | `null` | COLOR_BALANCE (lift/gamma/gain or offset/power/slope), CURVES, HUE_CORRECT, BRIGHT_CONTRAST, WHITE_BALANCE, TONEMAP, MASK; for sound strips SOUND_EQUALIZER, PITCH, ECHO. |
| `modifier` | `string?` | — | `null` | Modifier name to update, or to give a new one; the type's name otherwise. |
| `colorBalance` | `object?` | — | `null` | COLOR_BALANCE primary grade; 1 is neutral. |
| `curves` | `object?` | — | `null` | CURVES: points per channel {"C": [[0, 0], [0.25, 0.2], [0.75, 0.8], [1, 1]], "R": [...]}; HUE_CORRECT uses H, S, V. |
| `settings` | `object?` | — | `null` | Other modifier settings by Python name: BRIGHT_CONTRAST {"bright": 5, "contrast": 10}, WHITE_BALANCE {"white_value": [r, g, b]}, TONEMAP {"tonemap_type": "RD_PHOTORECEPTOR", "intensity": 0}. |
| `maskStrip` | `string?` | — | `null` | Strip whose picture masks the modifier. |
| `mute` | `boolean?` | — | `null` | Mute or unmute the modifier. |
| `remove` | `string?` | — | `null` | Modifier name to remove. |
| `clear` | `boolean` | — | `false` | Remove every modifier from the strip. |

<details><summary>JSON Schema</summary>

```json
{
  "type": "object",
  "properties": {
    "name": {
      "description": "Strip to grade.",
      "type": "string"
    },
    "type": {
      "description": "COLOR_BALANCE (lift/gamma/gain or offset/power/slope), CURVES, HUE_CORRECT, BRIGHT_CONTRAST, WHITE_BALANCE, TONEMAP, MASK; for sound strips SOUND_EQUALIZER, PITCH, ECHO.",
      "type": [
        "string",
        "null"
      ],
      "default": null
    },
    "modifier": {
      "description": "Modifier name to update, or to give a new one; the type\u0027s name otherwise.",
      "type": [
        "string",
        "null"
      ],
      "default": null
    },
    "colorBalance": {
      "description": "COLOR_BALANCE primary grade; 1 is neutral.",
      "type": [
        "object",
        "null"
      ],
      "properties": {
        "method": {
          "description": "LIFT_GAMMA_GAIN or OFFSET_POWER_SLOPE.",
          "type": [
            "string",
            "null"
          ]
        },
        "lift": {
          "type": [
            "array",
            "null"
          ],
          "items": {
            "type": "number"
          }
        },
        "gamma": {
          "type": [
            "array",
            "null"
          ],
          "items": {
            "type": "number"
          }
        },
        "gain": {
          "type": [
            "array",
            "null"
          ],
          "items": {
            "type": "number"
          }
        },
        "offset": {
          "type": [
            "array",
            "null"
          ],
          "items": {
            "type": "number"
          }
        },
        "power": {
          "type": [
            "array",
            "null"
          ],
          "items": {
            "type": "number"
          }
        },
        "slope": {
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
    "curves": {
      "description": "CURVES: points per channel {\u0022C\u0022: [[0, 0], [0.25, 0.2], [0.75, 0.8], [1, 1]], \u0022R\u0022: [...]}; HUE_CORRECT uses H, S, V.",
      "type": [
        "object",
        "null"
      ],
      "default": null
    },
    "settings": {
      "description": "Other modifier settings by Python name: BRIGHT_CONTRAST {\u0022bright\u0022: 5, \u0022contrast\u0022: 10}, WHITE_BALANCE {\u0022white_value\u0022: [r, g, b]}, TONEMAP {\u0022tonemap_type\u0022: \u0022RD_PHOTORECEPTOR\u0022, \u0022intensity\u0022: 0}.",
      "type": [
        "object",
        "null"
      ],
      "default": null
    },
    "maskStrip": {
      "description": "Strip whose picture masks the modifier.",
      "type": [
        "string",
        "null"
      ],
      "default": null
    },
    "mute": {
      "description": "Mute or unmute the modifier.",
      "type": [
        "boolean",
        "null"
      ],
      "default": null
    },
    "remove": {
      "description": "Modifier name to remove.",
      "type": [
        "string",
        "null"
      ],
      "default": null
    },
    "clear": {
      "description": "Remove every modifier from the strip.",
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

## <code>blender_<wbr>sequencer_<wbr>meta</code>

Groups strips into a meta strip, or separates one, through Blender's own operators; needs the Video Sequencer editor open in Blender.

*only adds · every call adds · stays inside Blender.*

| Parameter | Type | Required | Default | Description |
| --- | --- | --- | --- | --- |
| `action` | `string` | — | `make` | make (the default) groups the named strips; separate unpacks the named meta strip. |
| `names` | `array?` | — | `null` | Strips to group, or the meta strip to separate. |
| `newName` | `string?` | — | `null` | make: name for the meta strip. |

<details><summary>JSON Schema</summary>

```json
{
  "type": "object",
  "properties": {
    "action": {
      "description": "make (the default) groups the named strips; separate unpacks the named meta strip.",
      "type": "string",
      "default": "make"
    },
    "names": {
      "description": "Strips to group, or the meta strip to separate.",
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
    "newName": {
      "description": "make: name for the meta strip.",
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

