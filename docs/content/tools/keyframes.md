# Keyframes

Part of the `animation` skill: these tools appear once it is loaded — by `blender_enable_skill` with `animation`, or by `find_tool` when a search returns one of them.

## <code>blender_<wbr>set_<wbr>frame</code>

Sets the current frame, the scene's frame range and the frame rate; returns the effective values.

*only adds · every call adds · stays inside Blender.*

| Parameter | Type | Required | Default | Description |
| --- | --- | --- | --- | --- |
| `frame` | `integer?` | — | `null` | Move the playhead to this frame. |
| `start` | `integer?` | — | `null` | First frame of the scene. |
| `end` | `integer?` | — | `null` | Last frame of the scene. |
| `fps` | `integer?` | — | `null` | Frames per second. |

<details><summary>JSON Schema</summary>

```json
{
  "type": "object",
  "properties": {
    "frame": {
      "description": "Move the playhead to this frame.",
      "type": [
        "integer",
        "null"
      ],
      "default": null
    },
    "start": {
      "description": "First frame of the scene.",
      "type": [
        "integer",
        "null"
      ],
      "default": null
    },
    "end": {
      "description": "Last frame of the scene.",
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
    }
  }
}
```

</details>

## <code>blender_<wbr>insert_<wbr>keyframe</code>

Keys properties of an object on a frame, optionally setting their values first (rotation in degrees). Without channels or values, keys location, rotation and scale.

*only adds · every call adds · stays inside Blender.*

| Parameter | Type | Required | Default | Description |
| --- | --- | --- | --- | --- |
| `name` | `string` | yes | — | Name of the object as shown in the outliner. |
| `frame` | `integer?` | — | `null` | Frame to key; the current frame otherwise. |
| `channels` | `array?` | — | `null` | Animated properties: location, rotation_euler, scale, or any data path such as data.energy for a light, data.lens for a camera, hide_render. |
| `values` | `object?` | — | `null` | Values to set before keying, by channel: {"location": [0, 0, 2], "rotation_euler": [0, 0, 90]} (degrees), {"data.energy": 500}. |
| `interpolation` | `string?` | — | `null` | CONSTANT, LINEAR, BEZIER (the default) or an eased kind: SINE, QUAD, CUBIC, QUART, QUINT, EXPO, CIRC, BACK, BOUNCE, ELASTIC. |

<details><summary>JSON Schema</summary>

```json
{
  "type": "object",
  "properties": {
    "name": {
      "description": "Name of the object as shown in the outliner.",
      "type": "string"
    },
    "frame": {
      "description": "Frame to key; the current frame otherwise.",
      "type": [
        "integer",
        "null"
      ],
      "default": null
    },
    "channels": {
      "description": "Animated properties: location, rotation_euler, scale, or any data path such as data.energy for a light, data.lens for a camera, hide_render.",
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
    "values": {
      "description": "Values to set before keying, by channel: {\u0022location\u0022: [0, 0, 2], \u0022rotation_euler\u0022: [0, 0, 90]} (degrees), {\u0022data.energy\u0022: 500}.",
      "type": [
        "object",
        "null"
      ],
      "default": null
    },
    "interpolation": {
      "description": "CONSTANT, LINEAR, BEZIER (the default) or an eased kind: SINE, QUAD, CUBIC, QUART, QUINT, EXPO, CIRC, BACK, BOUNCE, ELASTIC.",
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

## <code>blender_<wbr>delete_<wbr>keyframes</code>

Deletes keys on a frame or channels; with neither, strips all animation from the object.

*may overwrite or delete · every call adds · stays inside Blender.*

| Parameter | Type | Required | Default | Description |
| --- | --- | --- | --- | --- |
| `name` | `string` | yes | — | Name of the object as shown in the outliner. |
| `frame` | `integer?` | — | `null` | Only keys on this frame. |
| `channels` | `array?` | — | `null` | Animated properties: location, rotation_euler, scale, or any data path such as data.energy for a light, data.lens for a camera, hide_render. |

<details><summary>JSON Schema</summary>

```json
{
  "type": "object",
  "properties": {
    "name": {
      "description": "Name of the object as shown in the outliner.",
      "type": "string"
    },
    "frame": {
      "description": "Only keys on this frame.",
      "type": [
        "integer",
        "null"
      ],
      "default": null
    },
    "channels": {
      "description": "Animated properties: location, rotation_euler, scale, or any data path such as data.energy for a light, data.lens for a camera, hide_render.",
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
    }
  },
  "required": [
    "name"
  ]
}
```

</details>

## <code>blender_<wbr>list_<wbr>keyframes</code>

Every animation curve of an object with its keys, interpolation and modifiers, plus its drivers.

*read-only · every call adds · stays inside Blender.*

| Parameter | Type | Required | Default | Description |
| --- | --- | --- | --- | --- |
| `name` | `string` | yes | — | Name of the object as shown in the outliner. |
| `channels` | `array?` | — | `null` | Animated properties: location, rotation_euler, scale, or any data path such as data.energy for a light, data.lens for a camera, hide_render. |

<details><summary>JSON Schema</summary>

```json
{
  "type": "object",
  "properties": {
    "name": {
      "description": "Name of the object as shown in the outliner.",
      "type": "string"
    },
    "channels": {
      "description": "Animated properties: location, rotation_euler, scale, or any data path such as data.energy for a light, data.lens for a camera, hide_render.",
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
    }
  },
  "required": [
    "name"
  ]
}
```

</details>

## <code>blender_<wbr>set_<wbr>interpolation</code>

Sets the interpolation and easing of keys, on all curves or a frame range.

*only adds · every call adds · stays inside Blender.*

| Parameter | Type | Required | Default | Description |
| --- | --- | --- | --- | --- |
| `name` | `string` | yes | — | Name of the object as shown in the outliner. |
| `interpolation` | `string` | — | `BEZIER` | CONSTANT, LINEAR, BEZIER (the default) or an eased kind: SINE, QUAD, CUBIC, QUART, QUINT, EXPO, CIRC, BACK, BOUNCE, ELASTIC. |
| `easing` | `string?` | — | `null` | For the eased kinds: EASE_IN, EASE_OUT, EASE_IN_OUT or AUTO. |
| `channels` | `array?` | — | `null` | Animated properties: location, rotation_euler, scale, or any data path such as data.energy for a light, data.lens for a camera, hide_render. |
| `fromFrame` | `integer?` | — | `null` | Only keys from this frame on. |
| `toFrame` | `integer?` | — | `null` | Only keys up to this frame. |

<details><summary>JSON Schema</summary>

```json
{
  "type": "object",
  "properties": {
    "name": {
      "description": "Name of the object as shown in the outliner.",
      "type": "string"
    },
    "interpolation": {
      "description": "CONSTANT, LINEAR, BEZIER (the default) or an eased kind: SINE, QUAD, CUBIC, QUART, QUINT, EXPO, CIRC, BACK, BOUNCE, ELASTIC.",
      "type": "string",
      "default": "BEZIER"
    },
    "easing": {
      "description": "For the eased kinds: EASE_IN, EASE_OUT, EASE_IN_OUT or AUTO.",
      "type": [
        "string",
        "null"
      ],
      "default": null
    },
    "channels": {
      "description": "Animated properties: location, rotation_euler, scale, or any data path such as data.energy for a light, data.lens for a camera, hide_render.",
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
    "fromFrame": {
      "description": "Only keys from this frame on.",
      "type": [
        "integer",
        "null"
      ],
      "default": null
    },
    "toFrame": {
      "description": "Only keys up to this frame.",
      "type": [
        "integer",
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

## <code>blender_<wbr>move_<wbr>keyframes</code>

Shifts keys in time and/or stretches their timing around a pivot frame.

*only adds · every call adds · stays inside Blender.*

| Parameter | Type | Required | Default | Description |
| --- | --- | --- | --- | --- |
| `name` | `string` | yes | — | Name of the object as shown in the outliner. |
| `offset` | `number` | — | `0` | Frames to shift the keys by; negative moves earlier. |
| `scale` | `number` | — | `1` | Factor to stretch the timing by around the pivot; 2 makes the motion twice as slow. |
| `pivot` | `integer?` | — | `null` | Frame the stretch is anchored at; the scene start otherwise. |
| `channels` | `array?` | — | `null` | Animated properties: location, rotation_euler, scale, or any data path such as data.energy for a light, data.lens for a camera, hide_render. |
| `fromFrame` | `integer?` | — | `null` | Only keys from this frame on. |
| `toFrame` | `integer?` | — | `null` | Only keys up to this frame. |

<details><summary>JSON Schema</summary>

```json
{
  "type": "object",
  "properties": {
    "name": {
      "description": "Name of the object as shown in the outliner.",
      "type": "string"
    },
    "offset": {
      "description": "Frames to shift the keys by; negative moves earlier.",
      "type": "number",
      "default": 0
    },
    "scale": {
      "description": "Factor to stretch the timing by around the pivot; 2 makes the motion twice as slow.",
      "type": "number",
      "default": 1
    },
    "pivot": {
      "description": "Frame the stretch is anchored at; the scene start otherwise.",
      "type": [
        "integer",
        "null"
      ],
      "default": null
    },
    "channels": {
      "description": "Animated properties: location, rotation_euler, scale, or any data path such as data.energy for a light, data.lens for a camera, hide_render.",
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
    "fromFrame": {
      "description": "Only keys from this frame on.",
      "type": [
        "integer",
        "null"
      ],
      "default": null
    },
    "toFrame": {
      "description": "Only keys up to this frame.",
      "type": [
        "integer",
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

## <code>blender_<wbr>add_<wbr>fcurve_<wbr>modifier</code>

Adds an F-Curve modifier to the object's animation curves: repeat with CYCLES, jitter with NOISE, and the rest.

*only adds · every call adds · stays inside Blender.*

| Parameter | Type | Required | Default | Description |
| --- | --- | --- | --- | --- |
| `name` | `string` | yes | — | Name of the object as shown in the outliner. |
| `type` | `string` | yes | — | CYCLES (repeat), NOISE, ENVELOPE, LIMITS, STEPPED, GENERATOR or FNGENERATOR. |
| `channels` | `array?` | — | `null` | Animated properties: location, rotation_euler, scale, or any data path such as data.energy for a light, data.lens for a camera, hide_render. |
| `settings` | `object?` | — | `null` | Modifier settings by Python name, e.g. NOISE {"strength": 0.5, "scale": 10}, CYCLES {"mode_after": "REPEAT_OFFSET"}. |

<details><summary>JSON Schema</summary>

```json
{
  "type": "object",
  "properties": {
    "name": {
      "description": "Name of the object as shown in the outliner.",
      "type": "string"
    },
    "type": {
      "description": "CYCLES (repeat), NOISE, ENVELOPE, LIMITS, STEPPED, GENERATOR or FNGENERATOR.",
      "type": "string"
    },
    "channels": {
      "description": "Animated properties: location, rotation_euler, scale, or any data path such as data.energy for a light, data.lens for a camera, hide_render.",
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
    "settings": {
      "description": "Modifier settings by Python name, e.g. NOISE {\u0022strength\u0022: 0.5, \u0022scale\u0022: 10}, CYCLES {\u0022mode_after\u0022: \u0022REPEAT_OFFSET\u0022}.",
      "type": [
        "object",
        "null"
      ],
      "default": null
    }
  },
  "required": [
    "name",
    "type"
  ]
}
```

</details>

## <code>blender_<wbr>add_<wbr>marker</code>

Adds a timeline marker, optionally binding a camera to it so the render switches cameras there.

*only adds · every call adds · stays inside Blender.*

| Parameter | Type | Required | Default | Description |
| --- | --- | --- | --- | --- |
| `frame` | `integer?` | — | `null` | Frame of the marker; the current frame otherwise. |
| `markerName` | `string?` | — | `null` | Marker name. |
| `camera` | `string?` | — | `null` | Camera that becomes active from this marker on (camera switching). |

<details><summary>JSON Schema</summary>

```json
{
  "type": "object",
  "properties": {
    "frame": {
      "description": "Frame of the marker; the current frame otherwise.",
      "type": [
        "integer",
        "null"
      ],
      "default": null
    },
    "markerName": {
      "description": "Marker name.",
      "type": [
        "string",
        "null"
      ],
      "default": null
    },
    "camera": {
      "description": "Camera that becomes active from this marker on (camera switching).",
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

