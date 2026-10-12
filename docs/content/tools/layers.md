# View layers and light linking

Part of the `rendering` skill: these tools appear once it is loaded — by `blender_enable_skill` with `rendering`, or by `find_tool` when a search returns one of them.

## <code>blender_<wbr>view_<wbr>layer</code>

Creates, configures, lists, activates or removes view layers: which collections are excluded, holdout or indirect-only, the render passes, cryptomatte (object, material, asset, levels), AOVs, light groups and their members, material and world overrides, per-layer samples. Returns the layer with its collection tree.

*only adds · every call adds · stays inside Blender.*

| Parameter | Type | Required | Default | Description |
| --- | --- | --- | --- | --- |
| `action` | `string` | — | `configure` | configure (the default; creates the layer when the name is new), create, activate, list, remove. |
| `name` | `string?` | — | `null` | View layer name; the active one otherwise. |
| `newName` | `string?` | — | `null` | Rename the layer. |
| `use` | `boolean?` | — | `null` | Render this layer; false skips it in renders. |
| `samples` | `integer?` | — | `null` | Per-layer sample override for Cycles; 0 uses the scene samples. |
| `collections` | `object?` | — | `null` | Which collections the layer renders, holds out or keeps for bounce light only. |
| `passes` | `object?` | — | `null` | Passes to switch: {"z": true, "normal": true, "mist": true, "vector": true, "ambient_occlusion": true, "diffuse_direct": true, "shadow_catcher": true, "cryptomatte_object": true}. |
| `cryptomatte` | `object?` | — | `null` | Cryptomatte passes. |
| `aovs` | `array?` | — | `null` | AOVs to create or keep: ["Mask"] or [{"name": "Wetness", "type": "VALUE"}]; shaders write them through an AOV Output node. |
| `removeAovs` | `array?` | — | `null` | AOV names to remove. |
| `lightGroups` | `array?` | — | `null` | Light groups to create: ["Key", "Fill", "Rim"]; each renders as its own Combined pass. |
| `removeLightGroups` | `array?` | — | `null` | Light group names to remove. |
| `lightGroupMembers` | `object?` | — | `null` | Which lights belong to which group: {"Key": ["Sun"], "Fill": ["Area", "World"]}. |
| `materialOverride` | `string?` | — | `null` | Material name that overrides every material on this layer (clay renders); empty removes it. |
| `freestyle` | `boolean?` | — | `null` | Render the layer with Freestyle lines. |
| `scene` | `string?` | — | `null` | Scene to act on; the current scene otherwise. |

<details><summary>JSON Schema</summary>

```json
{
  "type": "object",
  "properties": {
    "action": {
      "description": "configure (the default; creates the layer when the name is new), create, activate, list, remove.",
      "type": "string",
      "default": "configure"
    },
    "name": {
      "description": "View layer name; the active one otherwise.",
      "type": [
        "string",
        "null"
      ],
      "default": null
    },
    "newName": {
      "description": "Rename the layer.",
      "type": [
        "string",
        "null"
      ],
      "default": null
    },
    "use": {
      "description": "Render this layer; false skips it in renders.",
      "type": [
        "boolean",
        "null"
      ],
      "default": null
    },
    "samples": {
      "description": "Per-layer sample override for Cycles; 0 uses the scene samples.",
      "type": [
        "integer",
        "null"
      ],
      "default": null
    },
    "collections": {
      "description": "Which collections the layer renders, holds out or keeps for bounce light only.",
      "type": [
        "object",
        "null"
      ],
      "properties": {
        "include": {
          "description": "Collections rendered normally; also clears holdout and indirect-only on them.",
          "type": [
            "array",
            "null"
          ],
          "items": {
            "type": [
              "string",
              "null"
            ]
          }
        },
        "exclude": {
          "description": "Collections left out of this layer.",
          "type": [
            "array",
            "null"
          ],
          "items": {
            "type": [
              "string",
              "null"
            ]
          }
        },
        "holdout": {
          "description": "Collections cut as transparent holes, for plates.",
          "type": [
            "array",
            "null"
          ],
          "items": {
            "type": [
              "string",
              "null"
            ]
          }
        },
        "indirectOnly": {
          "description": "Collections that only contribute bounce light and reflections.",
          "type": [
            "array",
            "null"
          ],
          "items": {
            "type": [
              "string",
              "null"
            ]
          }
        },
        "reset": {
          "description": "Clear every exclusion, holdout and indirect-only first.",
          "type": [
            "boolean",
            "null"
          ]
        }
      },
      "default": null
    },
    "passes": {
      "description": "Passes to switch: {\u0022z\u0022: true, \u0022normal\u0022: true, \u0022mist\u0022: true, \u0022vector\u0022: true, \u0022ambient_occlusion\u0022: true, \u0022diffuse_direct\u0022: true, \u0022shadow_catcher\u0022: true, \u0022cryptomatte_object\u0022: true}.",
      "type": [
        "object",
        "null"
      ],
      "default": null
    },
    "cryptomatte": {
      "description": "Cryptomatte passes.",
      "type": [
        "object",
        "null"
      ],
      "properties": {
        "object": {
          "description": "Matte by object.",
          "type": [
            "boolean",
            "null"
          ]
        },
        "material": {
          "description": "Matte by material.",
          "type": [
            "boolean",
            "null"
          ]
        },
        "asset": {
          "description": "Matte by asset (the linked file).",
          "type": [
            "boolean",
            "null"
          ]
        },
        "levels": {
          "description": "Levels, an even number 2 to 16; 6 keeps three overlapping objects per pixel.",
          "type": [
            "integer",
            "null"
          ]
        },
        "accurate": {
          "description": "Accurate mode for exact coverage.",
          "type": [
            "boolean",
            "null"
          ]
        }
      },
      "default": null
    },
    "aovs": {
      "description": "AOVs to create or keep: [\u0022Mask\u0022] or [{\u0022name\u0022: \u0022Wetness\u0022, \u0022type\u0022: \u0022VALUE\u0022}]; shaders write them through an AOV Output node.",
      "type": [
        "array",
        "null"
      ],
      "default": null
    },
    "removeAovs": {
      "description": "AOV names to remove.",
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
    "lightGroups": {
      "description": "Light groups to create: [\u0022Key\u0022, \u0022Fill\u0022, \u0022Rim\u0022]; each renders as its own Combined pass.",
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
    "removeLightGroups": {
      "description": "Light group names to remove.",
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
    "lightGroupMembers": {
      "description": "Which lights belong to which group: {\u0022Key\u0022: [\u0022Sun\u0022], \u0022Fill\u0022: [\u0022Area\u0022, \u0022World\u0022]}.",
      "type": [
        "object",
        "null"
      ],
      "default": null
    },
    "materialOverride": {
      "description": "Material name that overrides every material on this layer (clay renders); empty removes it.",
      "type": [
        "string",
        "null"
      ],
      "default": null
    },
    "freestyle": {
      "description": "Render the layer with Freestyle lines.",
      "type": [
        "boolean",
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

## <code>blender_<wbr>light_<wbr>linking</code>

Cycles light and shadow linking: which objects or collections a light illuminates (include: only these; exclude: all but these) and which cast its shadow. Returns the receiver and blocker lists with their states.

*only adds · every call adds · stays inside Blender.*

| Parameter | Type | Required | Default | Description |
| --- | --- | --- | --- | --- |
| `light` | `string` | yes | — | The light object (or emissive mesh) whose reach changes. |
| `receivers` | `array?` | — | `null` | Objects or collections that receive its light. |
| `receiverMode` | `string` | — | `include` | include: the light reaches only the receivers; exclude: everything except them. |
| `blockers` | `array?` | — | `null` | Objects or collections that cast its shadow. |
| `blockerMode` | `string` | — | `include` | include: only the blockers cast this light's shadow; exclude: everything except them. |
| `clear` | `boolean` | — | `false` | Remove the light's receiver and blocker collections first. |

<details><summary>JSON Schema</summary>

```json
{
  "type": "object",
  "properties": {
    "light": {
      "description": "The light object (or emissive mesh) whose reach changes.",
      "type": "string"
    },
    "receivers": {
      "description": "Objects or collections that receive its light.",
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
    "receiverMode": {
      "description": "include: the light reaches only the receivers; exclude: everything except them.",
      "type": "string",
      "default": "include"
    },
    "blockers": {
      "description": "Objects or collections that cast its shadow.",
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
    "blockerMode": {
      "description": "include: only the blockers cast this light\u0027s shadow; exclude: everything except them.",
      "type": "string",
      "default": "include"
    },
    "clear": {
      "description": "Remove the light\u0027s receiver and blocker collections first.",
      "type": "boolean",
      "default": false
    }
  },
  "required": [
    "light"
  ]
}
```

</details>

