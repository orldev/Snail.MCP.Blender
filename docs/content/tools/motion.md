# Paths and drivers

Part of the `animation` skill: these tools appear once it is loaded — by `blender_enable_skill` with `animation`, or by `find_tool` when a search returns one of them.

## <code>blender_<wbr>follow_<wbr>path</code>

Makes an object travel along a curve over a frame range with a Follow Path constraint and a linear path animation.

*only adds · every call adds · stays inside Blender.*

| Parameter | Type | Required | Default | Description |
| --- | --- | --- | --- | --- |
| `name` | `string` | yes | — | Name of the object as shown in the outliner. |
| `curve` | `string` | yes | — | Name of the curve object to travel along. |
| `start` | `integer?` | — | `null` | Frame the travel starts on; the scene start otherwise. |
| `duration` | `integer?` | — | `null` | Frames the travel takes; up to the scene end otherwise. |
| `follow` | `boolean` | — | `true` | Turn the object to face along the curve. |
| `forwardAxis` | `string?` | — | `null` | Axis that points along the curve: FORWARD_X, FORWARD_Y, FORWARD_Z, TRACK_NEGATIVE_X, TRACK_NEGATIVE_Y, TRACK_NEGATIVE_Z. |

<details><summary>JSON Schema</summary>

```json
{
  "type": "object",
  "properties": {
    "name": {
      "description": "Name of the object as shown in the outliner.",
      "type": "string"
    },
    "curve": {
      "description": "Name of the curve object to travel along.",
      "type": "string"
    },
    "start": {
      "description": "Frame the travel starts on; the scene start otherwise.",
      "type": [
        "integer",
        "null"
      ],
      "default": null
    },
    "duration": {
      "description": "Frames the travel takes; up to the scene end otherwise.",
      "type": [
        "integer",
        "null"
      ],
      "default": null
    },
    "follow": {
      "description": "Turn the object to face along the curve.",
      "type": "boolean",
      "default": true
    },
    "forwardAxis": {
      "description": "Axis that points along the curve: FORWARD_X, FORWARD_Y, FORWARD_Z, TRACK_NEGATIVE_X, TRACK_NEGATIVE_Y, TRACK_NEGATIVE_Z.",
      "type": [
        "string",
        "null"
      ],
      "default": null
    }
  },
  "required": [
    "name",
    "curve"
  ]
}
```

</details>

## <code>blender_<wbr>add_<wbr>driver</code>

Drives a property with a Python expression over variables read from other objects and the frame.

*only adds · every call adds · stays inside Blender.*

| Parameter | Type | Required | Default | Description |
| --- | --- | --- | --- | --- |
| `name` | `string` | yes | — | Name of the object as shown in the outliner. |
| `dataPath` | `string` | yes | — | Property to drive, e.g. location, rotation_euler, scale, data.energy, modifiers["Array"].count. |
| `index` | `integer?` | — | `null` | Component of a vector property: 0 for x, 1 for y, 2 for z; empty for scalars. |
| `expression` | `string` | — | `frame` | Python expression over the variables and frame, e.g. sin(frame / 10) or dist * 2. |
| `variables` | `array?` | — | `null` | Variables as [{"name": "dist", "type": "SINGLE_PROP", "object": "Cube", "data_path": "location[0]"}] or type TRANSFORMS with transform_type LOC_X, ROT_Z, SCALE_Y. |

<details><summary>JSON Schema</summary>

```json
{
  "type": "object",
  "properties": {
    "name": {
      "description": "Name of the object as shown in the outliner.",
      "type": "string"
    },
    "dataPath": {
      "description": "Property to drive, e.g. location, rotation_euler, scale, data.energy, modifiers[\u0022Array\u0022].count.",
      "type": "string"
    },
    "index": {
      "description": "Component of a vector property: 0 for x, 1 for y, 2 for z; empty for scalars.",
      "type": [
        "integer",
        "null"
      ],
      "default": null
    },
    "expression": {
      "description": "Python expression over the variables and frame, e.g. sin(frame / 10) or dist * 2.",
      "type": "string",
      "default": "frame"
    },
    "variables": {
      "description": "Variables as [{\u0022name\u0022: \u0022dist\u0022, \u0022type\u0022: \u0022SINGLE_PROP\u0022, \u0022object\u0022: \u0022Cube\u0022, \u0022data_path\u0022: \u0022location[0]\u0022}] or type TRANSFORMS with transform_type LOC_X, ROT_Z, SCALE_Y.",
      "type": [
        "array",
        "null"
      ],
      "default": null
    }
  },
  "required": [
    "name",
    "dataPath"
  ]
}
```

</details>

