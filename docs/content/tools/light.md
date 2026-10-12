# Light

## <code>blender_<wbr>add_<wbr>light</code>

Adds a light of a type with position, power, colour and the type's own settings (cone for SPOT, size for AREA, angle for SUN, radius for POINT and SPOT).

*only adds · every call adds · stays inside Blender.*

| Parameter | Type | Required | Default | Description |
| --- | --- | --- | --- | --- |
| `type` | `string` | yes | — | POINT, SUN, SPOT or AREA. |
| `name` | `string?` | — | `null` | Name for the light object. |
| `location` | `array?` | — | `null` | [x, y, z] in metres. |
| `rotation` | `array?` | — | `null` | [x, y, z] Euler rotation in degrees. |
| `energy` | `number?` | — | `null` | Power in watts (SUN: irradiance in W/m²). |
| `color` | `array?` | — | `null` | [r, g, b] with components from 0 to 1. |
| `shadow` | `boolean?` | — | `null` | Cast shadows. |
| `spotSize` | `number?` | — | `null` | SPOT: cone angle in degrees. |
| `spotBlend` | `number?` | — | `null` | SPOT: softness of the cone edge, 0 to 1. |
| `size` | `number?` | — | `null` | AREA: size of the emitting square in metres. |
| `angle` | `number?` | — | `null` | SUN: angular diameter in degrees; larger softens shadows. |
| `radius` | `number?` | — | `null` | POINT and SPOT: radius of the emitter in metres; larger softens shadows. |
| `temperature` | `number?` | — | `null` | Colour temperature in kelvin instead of a colour: 3200 tungsten, 5600 daylight, 6500 neutral, 8000 shade. |
| `shape` | `string?` | — | `null` | AREA: SQUARE, RECTANGLE, DISK or ELLIPSE. |
| `sizeY` | `number?` | — | `null` | AREA: second size in metres for RECTANGLE and ELLIPSE. |
| `spread` | `number?` | — | `null` | AREA: spread angle in degrees, 1 to 180; 180 is a soft box, 30 a narrow strip. |
| `iesPath` | `string?` | — | `null` | Absolute path of an IES profile for a real fixture's beam; empty string removes it. |

<details><summary>JSON Schema</summary>

```json
{
  "type": "object",
  "properties": {
    "type": {
      "description": "POINT, SUN, SPOT or AREA.",
      "type": "string"
    },
    "name": {
      "description": "Name for the light object.",
      "type": [
        "string",
        "null"
      ],
      "default": null
    },
    "location": {
      "description": "[x, y, z] in metres.",
      "type": [
        "array",
        "null"
      ],
      "items": {
        "type": "number"
      },
      "default": null
    },
    "rotation": {
      "description": "[x, y, z] Euler rotation in degrees.",
      "type": [
        "array",
        "null"
      ],
      "items": {
        "type": "number"
      },
      "default": null
    },
    "energy": {
      "description": "Power in watts (SUN: irradiance in W/m\u00B2).",
      "type": [
        "number",
        "null"
      ],
      "default": null
    },
    "color": {
      "description": "[r, g, b] with components from 0 to 1.",
      "type": [
        "array",
        "null"
      ],
      "items": {
        "type": "number"
      },
      "default": null
    },
    "shadow": {
      "description": "Cast shadows.",
      "type": [
        "boolean",
        "null"
      ],
      "default": null
    },
    "spotSize": {
      "description": "SPOT: cone angle in degrees.",
      "type": [
        "number",
        "null"
      ],
      "default": null
    },
    "spotBlend": {
      "description": "SPOT: softness of the cone edge, 0 to 1.",
      "type": [
        "number",
        "null"
      ],
      "default": null
    },
    "size": {
      "description": "AREA: size of the emitting square in metres.",
      "type": [
        "number",
        "null"
      ],
      "default": null
    },
    "angle": {
      "description": "SUN: angular diameter in degrees; larger softens shadows.",
      "type": [
        "number",
        "null"
      ],
      "default": null
    },
    "radius": {
      "description": "POINT and SPOT: radius of the emitter in metres; larger softens shadows.",
      "type": [
        "number",
        "null"
      ],
      "default": null
    },
    "temperature": {
      "description": "Colour temperature in kelvin instead of a colour: 3200 tungsten, 5600 daylight, 6500 neutral, 8000 shade.",
      "type": [
        "number",
        "null"
      ],
      "default": null
    },
    "shape": {
      "description": "AREA: SQUARE, RECTANGLE, DISK or ELLIPSE.",
      "type": [
        "string",
        "null"
      ],
      "default": null
    },
    "sizeY": {
      "description": "AREA: second size in metres for RECTANGLE and ELLIPSE.",
      "type": [
        "number",
        "null"
      ],
      "default": null
    },
    "spread": {
      "description": "AREA: spread angle in degrees, 1 to 180; 180 is a soft box, 30 a narrow strip.",
      "type": [
        "number",
        "null"
      ],
      "default": null
    },
    "iesPath": {
      "description": "Absolute path of an IES profile for a real fixture\u0027s beam; empty string removes it.",
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

## <code>blender_<wbr>set_<wbr>light</code>

Changes a light's power, colour, shadows and type-specific settings.

*only adds · every call adds · stays inside Blender.*

| Parameter | Type | Required | Default | Description |
| --- | --- | --- | --- | --- |
| `name` | `string` | yes | — | Name of the light object. |
| `energy` | `number?` | — | `null` | Power in watts (SUN: irradiance in W/m²). |
| `color` | `array?` | — | `null` | [r, g, b] with components from 0 to 1. |
| `shadow` | `boolean?` | — | `null` | Cast shadows. |
| `spotSize` | `number?` | — | `null` | SPOT: cone angle in degrees. |
| `spotBlend` | `number?` | — | `null` | SPOT: softness of the cone edge, 0 to 1. |
| `size` | `number?` | — | `null` | AREA: size of the emitting square in metres. |
| `angle` | `number?` | — | `null` | SUN: angular diameter in degrees; larger softens shadows. |
| `radius` | `number?` | — | `null` | POINT and SPOT: radius of the emitter in metres; larger softens shadows. |
| `temperature` | `number?` | — | `null` | Colour temperature in kelvin instead of a colour: 3200 tungsten, 5600 daylight, 6500 neutral, 8000 shade. |
| `shape` | `string?` | — | `null` | AREA: SQUARE, RECTANGLE, DISK or ELLIPSE. |
| `sizeY` | `number?` | — | `null` | AREA: second size in metres for RECTANGLE and ELLIPSE. |
| `spread` | `number?` | — | `null` | AREA: spread angle in degrees, 1 to 180; 180 is a soft box, 30 a narrow strip. |
| `iesPath` | `string?` | — | `null` | Absolute path of an IES profile for a real fixture's beam; empty string removes it. |

<details><summary>JSON Schema</summary>

```json
{
  "type": "object",
  "properties": {
    "name": {
      "description": "Name of the light object.",
      "type": "string"
    },
    "energy": {
      "description": "Power in watts (SUN: irradiance in W/m\u00B2).",
      "type": [
        "number",
        "null"
      ],
      "default": null
    },
    "color": {
      "description": "[r, g, b] with components from 0 to 1.",
      "type": [
        "array",
        "null"
      ],
      "items": {
        "type": "number"
      },
      "default": null
    },
    "shadow": {
      "description": "Cast shadows.",
      "type": [
        "boolean",
        "null"
      ],
      "default": null
    },
    "spotSize": {
      "description": "SPOT: cone angle in degrees.",
      "type": [
        "number",
        "null"
      ],
      "default": null
    },
    "spotBlend": {
      "description": "SPOT: softness of the cone edge, 0 to 1.",
      "type": [
        "number",
        "null"
      ],
      "default": null
    },
    "size": {
      "description": "AREA: size of the emitting square in metres.",
      "type": [
        "number",
        "null"
      ],
      "default": null
    },
    "angle": {
      "description": "SUN: angular diameter in degrees; larger softens shadows.",
      "type": [
        "number",
        "null"
      ],
      "default": null
    },
    "radius": {
      "description": "POINT and SPOT: radius of the emitter in metres; larger softens shadows.",
      "type": [
        "number",
        "null"
      ],
      "default": null
    },
    "temperature": {
      "description": "Colour temperature in kelvin instead of a colour: 3200 tungsten, 5600 daylight, 6500 neutral, 8000 shade.",
      "type": [
        "number",
        "null"
      ],
      "default": null
    },
    "shape": {
      "description": "AREA: SQUARE, RECTANGLE, DISK or ELLIPSE.",
      "type": [
        "string",
        "null"
      ],
      "default": null
    },
    "sizeY": {
      "description": "AREA: second size in metres for RECTANGLE and ELLIPSE.",
      "type": [
        "number",
        "null"
      ],
      "default": null
    },
    "spread": {
      "description": "AREA: spread angle in degrees, 1 to 180; 180 is a soft box, 30 a narrow strip.",
      "type": [
        "number",
        "null"
      ],
      "default": null
    },
    "iesPath": {
      "description": "Absolute path of an IES profile for a real fixture\u0027s beam; empty string removes it.",
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

