# Curves

Part of the `modeling` skill: these tools appear once it is loaded — by `blender_enable_skill` with `modeling`, or by `find_tool` when a search returns one of them.

## <code>blender_<wbr>add_<wbr>curve</code>

Adds a curve: bezier, nurbs or poly drawn through given points, or a circle or path primitive, with optional thickness (bevelDepth) that turns it into a tube.

*only adds · every call adds · stays inside Blender.*

| Parameter | Type | Required | Default | Description |
| --- | --- | --- | --- | --- |
| `kind` | `string` | — | `bezier` | bezier, nurbs or poly (drawn through points), circle or path (Blender's primitives). |
| `name` | `string?` | — | `null` | Name for the curve object. |
| `points` | `array?` | — | `null` | Control points as [[x, y, z], ...] for bezier, nurbs and poly; at least two. |
| `closed` | `boolean` | — | `false` | Close the curve into a loop. |
| `location` | `array?` | — | `null` | [x, y, z] in metres. |
| `size` | `number?` | — | `null` | Radius of a circle or length of a path. |
| `bevelDepth` | `number?` | — | `null` | Thickness of the curve as a round tube, in metres; 0 keeps it a line. |
| `extrude` | `number?` | — | `null` | Extrude the curve into a flat ribbon of this half-height, in metres. |
| `resolutionU` | `integer?` | — | `null` | Segments between control points; more is smoother. |

<details><summary>JSON Schema</summary>

```json
{
  "type": "object",
  "properties": {
    "kind": {
      "description": "bezier, nurbs or poly (drawn through points), circle or path (Blender\u0027s primitives).",
      "type": "string",
      "default": "bezier"
    },
    "name": {
      "description": "Name for the curve object.",
      "type": [
        "string",
        "null"
      ],
      "default": null
    },
    "points": {
      "description": "Control points as [[x, y, z], ...] for bezier, nurbs and poly; at least two.",
      "type": [
        "array",
        "null"
      ],
      "items": {
        "type": [
          "array",
          "null"
        ],
        "items": {
          "type": "number"
        }
      },
      "default": null
    },
    "closed": {
      "description": "Close the curve into a loop.",
      "type": "boolean",
      "default": false
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
    "size": {
      "description": "Radius of a circle or length of a path.",
      "type": [
        "number",
        "null"
      ],
      "default": null
    },
    "bevelDepth": {
      "description": "Thickness of the curve as a round tube, in metres; 0 keeps it a line.",
      "type": [
        "number",
        "null"
      ],
      "default": null
    },
    "extrude": {
      "description": "Extrude the curve into a flat ribbon of this half-height, in metres.",
      "type": [
        "number",
        "null"
      ],
      "default": null
    },
    "resolutionU": {
      "description": "Segments between control points; more is smoother.",
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

## <code>blender_<wbr>set_<wbr>curve</code>

Changes a curve's thickness, extrusion, resolution, fill, and the objects used as bevel profile or taper.

*only adds · every call adds · stays inside Blender.*

| Parameter | Type | Required | Default | Description |
| --- | --- | --- | --- | --- |
| `name` | `string` | yes | — | Name of the curve object. |
| `bevelDepth` | `number?` | — | `null` | Thickness of the curve as a round tube, in metres; 0 keeps it a line. |
| `bevelResolution` | `integer?` | — | `null` | Segments around a round bevel; more is smoother. |
| `extrude` | `number?` | — | `null` | Extrude the curve into a flat ribbon of this half-height, in metres. |
| `resolutionU` | `integer?` | — | `null` | Segments between control points; more is smoother. |
| `fillMode` | `string?` | — | `null` | How closed 2D curves fill: NONE, BACK, FRONT, BOTH (FULL, HALF, FRONT, BACK for 3D). |
| `bevelObject` | `string?` | — | `null` | Curve object swept along this one as its cross-section; empty string removes it. |
| `taperObject` | `string?` | — | `null` | Curve object scaling the thickness along the length; empty string removes it. |

<details><summary>JSON Schema</summary>

```json
{
  "type": "object",
  "properties": {
    "name": {
      "description": "Name of the curve object.",
      "type": "string"
    },
    "bevelDepth": {
      "description": "Thickness of the curve as a round tube, in metres; 0 keeps it a line.",
      "type": [
        "number",
        "null"
      ],
      "default": null
    },
    "bevelResolution": {
      "description": "Segments around a round bevel; more is smoother.",
      "type": [
        "integer",
        "null"
      ],
      "default": null
    },
    "extrude": {
      "description": "Extrude the curve into a flat ribbon of this half-height, in metres.",
      "type": [
        "number",
        "null"
      ],
      "default": null
    },
    "resolutionU": {
      "description": "Segments between control points; more is smoother.",
      "type": [
        "integer",
        "null"
      ],
      "default": null
    },
    "fillMode": {
      "description": "How closed 2D curves fill: NONE, BACK, FRONT, BOTH (FULL, HALF, FRONT, BACK for 3D).",
      "type": [
        "string",
        "null"
      ],
      "default": null
    },
    "bevelObject": {
      "description": "Curve object swept along this one as its cross-section; empty string removes it.",
      "type": [
        "string",
        "null"
      ],
      "default": null
    },
    "taperObject": {
      "description": "Curve object scaling the thickness along the length; empty string removes it.",
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

## <code>blender_<wbr>convert_<wbr>to_<wbr>mesh</code>

Converts a curve, text or metaball object into a mesh, keeping the original on request.

*may overwrite or delete · every call adds · stays inside Blender.*

| Parameter | Type | Required | Default | Description |
| --- | --- | --- | --- | --- |
| `name` | `string` | yes | — | Name of the curve, text or metaball object. |
| `keepOriginal` | `boolean` | — | `false` | Keep the original and convert a copy. |

<details><summary>JSON Schema</summary>

```json
{
  "type": "object",
  "properties": {
    "name": {
      "description": "Name of the curve, text or metaball object.",
      "type": "string"
    },
    "keepOriginal": {
      "description": "Keep the original and convert a copy.",
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

