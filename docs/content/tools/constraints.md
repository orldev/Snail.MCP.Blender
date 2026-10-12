# Constraints

Part of the `animation` skill: these tools appear once it is loaded — by `blender_enable_skill` with `animation`, or by `find_tool` when a search returns one of them.

## <code>blender_<wbr>add_<wbr>constraint</code>

Adds a constraint of any type to an object or a bone with its settings: aim a camera with TRACK_TO, follow with COPY_LOCATION, limit motion, IK for rigs. Returns the effective settings.

*only adds · every call adds · stays inside Blender.*

| Parameter | Type | Required | Default | Description |
| --- | --- | --- | --- | --- |
| `name` | `string` | yes | — | Name of the object as shown in the outliner. |
| `type` | `string` | yes | — | TRACK_TO, DAMPED_TRACK, COPY_LOCATION, COPY_ROTATION, COPY_TRANSFORMS, LIMIT_LOCATION, LIMIT_ROTATION, LIMIT_DISTANCE, CHILD_OF, FOLLOW_PATH, IK, STRETCH_TO, SHRINKWRAP and the rest; blender_describe_constraint lists them. |
| `settings` | `object?` | — | `null` | Settings by Python name, e.g. {"target": "Camera"}, TRACK_TO {"target": "Cube", "track_axis": "TRACK_NEGATIVE_Z", "up_axis": "UP_Y"}, IK {"target": "Rig", "subtarget": "Hand.IK", "chain_count": 2}; objects are named by their name, bones by subtarget. blender_describe_constraint documents them. |
| `bone` | `string?` | — | `null` | Bone of an armature to constrain instead of the object (IK, copy rotation between bones). |
| `constraintName` | `string?` | — | `null` | Name for the constraint. |
| `influence` | `number?` | — | `null` | Strength 0 to 1. |

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
      "description": "TRACK_TO, DAMPED_TRACK, COPY_LOCATION, COPY_ROTATION, COPY_TRANSFORMS, LIMIT_LOCATION, LIMIT_ROTATION, LIMIT_DISTANCE, CHILD_OF, FOLLOW_PATH, IK, STRETCH_TO, SHRINKWRAP and the rest; blender_describe_constraint lists them.",
      "type": "string"
    },
    "settings": {
      "description": "Settings by Python name, e.g. {\u0022target\u0022: \u0022Camera\u0022}, TRACK_TO {\u0022target\u0022: \u0022Cube\u0022, \u0022track_axis\u0022: \u0022TRACK_NEGATIVE_Z\u0022, \u0022up_axis\u0022: \u0022UP_Y\u0022}, IK {\u0022target\u0022: \u0022Rig\u0022, \u0022subtarget\u0022: \u0022Hand.IK\u0022, \u0022chain_count\u0022: 2}; objects are named by their name, bones by subtarget. blender_describe_constraint documents them.",
      "type": [
        "object",
        "null"
      ],
      "default": null
    },
    "bone": {
      "description": "Bone of an armature to constrain instead of the object (IK, copy rotation between bones).",
      "type": [
        "string",
        "null"
      ],
      "default": null
    },
    "constraintName": {
      "description": "Name for the constraint.",
      "type": [
        "string",
        "null"
      ],
      "default": null
    },
    "influence": {
      "description": "Strength 0 to 1.",
      "type": [
        "number",
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

## <code>blender_<wbr>update_<wbr>constraint</code>

Changes settings, influence, enabled state or name of a constraint.

*only adds · every call adds · stays inside Blender.*

| Parameter | Type | Required | Default | Description |
| --- | --- | --- | --- | --- |
| `name` | `string` | yes | — | Name of the object as shown in the outliner. |
| `constraint` | `string` | yes | — | Constraint name as blender_object_info lists it. |
| `settings` | `object?` | — | `null` | Settings by Python name, e.g. {"target": "Camera"}, TRACK_TO {"target": "Cube", "track_axis": "TRACK_NEGATIVE_Z", "up_axis": "UP_Y"}, IK {"target": "Rig", "subtarget": "Hand.IK", "chain_count": 2}; objects are named by their name, bones by subtarget. blender_describe_constraint documents them. |
| `bone` | `string?` | — | `null` | Bone that owns the constraint, for bone constraints. |
| `influence` | `number?` | — | `null` | Strength 0 to 1. |
| `enabled` | `boolean?` | — | `null` | Switch it on or off without removing it. |
| `newName` | `string?` | — | `null` | New name. |

<details><summary>JSON Schema</summary>

```json
{
  "type": "object",
  "properties": {
    "name": {
      "description": "Name of the object as shown in the outliner.",
      "type": "string"
    },
    "constraint": {
      "description": "Constraint name as blender_object_info lists it.",
      "type": "string"
    },
    "settings": {
      "description": "Settings by Python name, e.g. {\u0022target\u0022: \u0022Camera\u0022}, TRACK_TO {\u0022target\u0022: \u0022Cube\u0022, \u0022track_axis\u0022: \u0022TRACK_NEGATIVE_Z\u0022, \u0022up_axis\u0022: \u0022UP_Y\u0022}, IK {\u0022target\u0022: \u0022Rig\u0022, \u0022subtarget\u0022: \u0022Hand.IK\u0022, \u0022chain_count\u0022: 2}; objects are named by their name, bones by subtarget. blender_describe_constraint documents them.",
      "type": [
        "object",
        "null"
      ],
      "default": null
    },
    "bone": {
      "description": "Bone that owns the constraint, for bone constraints.",
      "type": [
        "string",
        "null"
      ],
      "default": null
    },
    "influence": {
      "description": "Strength 0 to 1.",
      "type": [
        "number",
        "null"
      ],
      "default": null
    },
    "enabled": {
      "description": "Switch it on or off without removing it.",
      "type": [
        "boolean",
        "null"
      ],
      "default": null
    },
    "newName": {
      "description": "New name.",
      "type": [
        "string",
        "null"
      ],
      "default": null
    }
  },
  "required": [
    "name",
    "constraint"
  ]
}
```

</details>

## <code>blender_<wbr>remove_<wbr>constraint</code>

Removes a constraint from an object or a bone.

*may overwrite or delete · every call adds · stays inside Blender.*

| Parameter | Type | Required | Default | Description |
| --- | --- | --- | --- | --- |
| `name` | `string` | yes | — | Name of the object as shown in the outliner. |
| `constraint` | `string` | yes | — | Constraint name. |
| `bone` | `string?` | — | `null` | Bone that owns the constraint, for bone constraints. |

<details><summary>JSON Schema</summary>

```json
{
  "type": "object",
  "properties": {
    "name": {
      "description": "Name of the object as shown in the outliner.",
      "type": "string"
    },
    "constraint": {
      "description": "Constraint name.",
      "type": "string"
    },
    "bone": {
      "description": "Bone that owns the constraint, for bone constraints.",
      "type": [
        "string",
        "null"
      ],
      "default": null
    }
  },
  "required": [
    "name",
    "constraint"
  ]
}
```

</details>

## <code>blender_<wbr>describe_<wbr>constraint</code>

Lists every constraint type, or documents one type's settings with types, defaults and enum values.

*read-only · every call adds · stays inside Blender.*

| Parameter | Type | Required | Default | Description |
| --- | --- | --- | --- | --- |
| `type` | `string?` | — | `null` | Constraint type to document; empty lists every type. |

<details><summary>JSON Schema</summary>

```json
{
  "type": "object",
  "properties": {
    "type": {
      "description": "Constraint type to document; empty lists every type.",
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

