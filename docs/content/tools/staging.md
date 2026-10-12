# Camera moves and light rigs

Part of the `rendering` skill: these tools appear once it is loaded — by `blender_enable_skill` with `rendering`, or by `find_tool` when a search returns one of them.

## <code>blender_<wbr>camera_<wbr>move</code>

Animates a camera move around a subject over a frame range: turntable, orbit, dolly, push-in or crane, with the camera aimed at the subject. Product videos and look-development spins in one call.

*only adds · every call adds · stays inside Blender.*

| Parameter | Type | Required | Default | Description |
| --- | --- | --- | --- | --- |
| `type` | `string` | yes | — | turntable (the subject spins on a pivot under a still camera), orbit (the camera circles the subject), dolly or push_in (the camera moves towards the subject), crane (the camera rises). |
| `target` | `string?` | — | `null` | Subject object. |
| `targets` | `array?` | — | `null` | Several subjects framed together. |
| `camera` | `string?` | — | `null` | Camera to move; the scene camera otherwise. Not used by turntable. |
| `start` | `integer?` | — | `null` | First frame; the scene start otherwise. |
| `end` | `integer?` | — | `null` | Last frame; the scene end otherwise. |
| `revolutions` | `number?` | — | `null` | turntable, orbit: full turns over the range; 1 by default. |
| `startAngle` | `number?` | — | `null` | Start angle around the subject in degrees. |
| `distance` | `number?` | — | `null` | orbit, dolly, crane: camera distance from the subject in metres; from its size otherwise. |
| `distanceEnd` | `number?` | — | `null` | dolly, push_in: distance at the end; half the start otherwise. |
| `height` | `number?` | — | `null` | orbit, dolly: camera height above the subject centre; crane: start height. |
| `heightEnd` | `number?` | — | `null` | crane: height at the end. |
| `interpolation` | `string?` | — | `null` | LINEAR (the default, for loops) or BEZIER (eases in and out). |
| `pivotName` | `string?` | — | `null` | Name of the pivot empty for turntable and orbit. |
| `scene` | `string?` | — | `null` | Scene to act on; the current scene otherwise. |

<details><summary>JSON Schema</summary>

```json
{
  "type": "object",
  "properties": {
    "type": {
      "description": "turntable (the subject spins on a pivot under a still camera), orbit (the camera circles the subject), dolly or push_in (the camera moves towards the subject), crane (the camera rises).",
      "type": "string"
    },
    "target": {
      "description": "Subject object.",
      "type": [
        "string",
        "null"
      ],
      "default": null
    },
    "targets": {
      "description": "Several subjects framed together.",
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
    "camera": {
      "description": "Camera to move; the scene camera otherwise. Not used by turntable.",
      "type": [
        "string",
        "null"
      ],
      "default": null
    },
    "start": {
      "description": "First frame; the scene start otherwise.",
      "type": [
        "integer",
        "null"
      ],
      "default": null
    },
    "end": {
      "description": "Last frame; the scene end otherwise.",
      "type": [
        "integer",
        "null"
      ],
      "default": null
    },
    "revolutions": {
      "description": "turntable, orbit: full turns over the range; 1 by default.",
      "type": [
        "number",
        "null"
      ],
      "default": null
    },
    "startAngle": {
      "description": "Start angle around the subject in degrees.",
      "type": [
        "number",
        "null"
      ],
      "default": null
    },
    "distance": {
      "description": "orbit, dolly, crane: camera distance from the subject in metres; from its size otherwise.",
      "type": [
        "number",
        "null"
      ],
      "default": null
    },
    "distanceEnd": {
      "description": "dolly, push_in: distance at the end; half the start otherwise.",
      "type": [
        "number",
        "null"
      ],
      "default": null
    },
    "height": {
      "description": "orbit, dolly: camera height above the subject centre; crane: start height.",
      "type": [
        "number",
        "null"
      ],
      "default": null
    },
    "heightEnd": {
      "description": "crane: height at the end.",
      "type": [
        "number",
        "null"
      ],
      "default": null
    },
    "interpolation": {
      "description": "LINEAR (the default, for loops) or BEZIER (eases in and out).",
      "type": [
        "string",
        "null"
      ],
      "default": null
    },
    "pivotName": {
      "description": "Name of the pivot empty for turntable and orbit.",
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
  },
  "required": [
    "type"
  ]
}
```

</details>

## <code>blender_<wbr>light_<wbr>rig</code>

Builds a three-point rig of area lights around a subject: key, fill and rim at studio angles, sized and powered from the subject's size, each with a colour temperature and aimed at the subject. Replaces a rig of the same name.

*only adds · every call adds · stays inside Blender.*

| Parameter | Type | Required | Default | Description |
| --- | --- | --- | --- | --- |
| `target` | `string?` | — | `null` | Subject object. |
| `targets` | `array?` | — | `null` | Several subjects lit together. |
| `name` | `string?` | — | `null` | Name prefix for the lights; Rig otherwise. |
| `roles` | `array?` | — | `null` | Lights to build: key, fill, rim; all three otherwise. |
| `keyEnergy` | `number?` | — | `null` | Key light power in watts; 1000 by default. |
| `fillRatio` | `number?` | — | `null` | Fill power as a fraction of the key; 0.35 by default (a 3:1 ratio). |
| `rimRatio` | `number?` | — | `null` | Rim power as a fraction of the key; 1.5 by default. |
| `keyAngle` | `number?` | — | `null` | Key light direction around the subject in degrees; 45 by default. |
| `distance` | `number?` | — | `null` | Distance of the lights from the subject in metres; from its size otherwise. |
| `keyTemperature` | `number?` | — | `null` | Key colour temperature in kelvin; 5600 by default. |
| `fillTemperature` | `number?` | — | `null` | Fill colour temperature in kelvin; 6500 by default. |
| `rimTemperature` | `number?` | — | `null` | Rim colour temperature in kelvin; 6500 by default. |
| `scene` | `string?` | — | `null` | Scene to act on; the current scene otherwise. |

<details><summary>JSON Schema</summary>

```json
{
  "type": "object",
  "properties": {
    "target": {
      "description": "Subject object.",
      "type": [
        "string",
        "null"
      ],
      "default": null
    },
    "targets": {
      "description": "Several subjects lit together.",
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
      "description": "Name prefix for the lights; Rig otherwise.",
      "type": [
        "string",
        "null"
      ],
      "default": null
    },
    "roles": {
      "description": "Lights to build: key, fill, rim; all three otherwise.",
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
    "keyEnergy": {
      "description": "Key light power in watts; 1000 by default.",
      "type": [
        "number",
        "null"
      ],
      "default": null
    },
    "fillRatio": {
      "description": "Fill power as a fraction of the key; 0.35 by default (a 3:1 ratio).",
      "type": [
        "number",
        "null"
      ],
      "default": null
    },
    "rimRatio": {
      "description": "Rim power as a fraction of the key; 1.5 by default.",
      "type": [
        "number",
        "null"
      ],
      "default": null
    },
    "keyAngle": {
      "description": "Key light direction around the subject in degrees; 45 by default.",
      "type": [
        "number",
        "null"
      ],
      "default": null
    },
    "distance": {
      "description": "Distance of the lights from the subject in metres; from its size otherwise.",
      "type": [
        "number",
        "null"
      ],
      "default": null
    },
    "keyTemperature": {
      "description": "Key colour temperature in kelvin; 5600 by default.",
      "type": [
        "number",
        "null"
      ],
      "default": null
    },
    "fillTemperature": {
      "description": "Fill colour temperature in kelvin; 6500 by default.",
      "type": [
        "number",
        "null"
      ],
      "default": null
    },
    "rimTemperature": {
      "description": "Rim colour temperature in kelvin; 6500 by default.",
      "type": [
        "number",
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

