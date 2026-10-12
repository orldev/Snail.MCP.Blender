# Physics

Part of the `physics` skill: these tools appear once it is loaded — by `blender_enable_skill` with `physics`, or by `find_tool` when a search returns one of them.

## <code>blender_<wbr>add_<wbr>physics</code>

Adds a simulation to an object: rigid body, cloth, soft body, collision, fluid or dynamic paint, with settings. Rigid bodies need the animation played or the cache baked to move.

*only adds · every call adds · stays inside Blender.*

| Parameter | Type | Required | Default | Description |
| --- | --- | --- | --- | --- |
| `name` | `string` | yes | — | Name of the object as shown in the outliner. |
| `type` | `string` | yes | — | rigid_body (falls and collides), rigid_body_passive (static obstacle), cloth, soft_body, collision (obstacle for cloth, soft bodies and particles), fluid_domain, fluid_flow, fluid_effector, dynamic_paint. |
| `settings` | `object?` | — | `null` | Settings by Python name on the physics block, e.g. rigid_body {"mass": 2, "collision_shape": "MESH"}, cloth {"quality": 8, "mass": 0.3}, fluid_domain {"domain_type": "LIQUID", "resolution_max": 64}, fluid_flow {"flow_type": "LIQUID", "flow_behavior": "INFLOW"}. |

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
      "description": "rigid_body (falls and collides), rigid_body_passive (static obstacle), cloth, soft_body, collision (obstacle for cloth, soft bodies and particles), fluid_domain, fluid_flow, fluid_effector, dynamic_paint.",
      "type": "string"
    },
    "settings": {
      "description": "Settings by Python name on the physics block, e.g. rigid_body {\u0022mass\u0022: 2, \u0022collision_shape\u0022: \u0022MESH\u0022}, cloth {\u0022quality\u0022: 8, \u0022mass\u0022: 0.3}, fluid_domain {\u0022domain_type\u0022: \u0022LIQUID\u0022, \u0022resolution_max\u0022: 64}, fluid_flow {\u0022flow_type\u0022: \u0022LIQUID\u0022, \u0022flow_behavior\u0022: \u0022INFLOW\u0022}.",
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

## <code>blender_<wbr>remove_<wbr>physics</code>

Removes a simulation or the particle systems from an object.

*may overwrite or delete · every call adds · stays inside Blender.*

| Parameter | Type | Required | Default | Description |
| --- | --- | --- | --- | --- |
| `name` | `string` | yes | — | Name of the object as shown in the outliner. |
| `type` | `string` | yes | — | Which physics to remove: a kind from blender_add_physics, or particles. |

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
      "description": "Which physics to remove: a kind from blender_add_physics, or particles.",
      "type": "string"
    }
  },
  "required": [
    "name",
    "type"
  ]
}
```

</details>

## <code>blender_<wbr>add_<wbr>particles</code>

Adds an emitter or hair particle system to a mesh with count, timing, length and further settings.

*only adds · every call adds · stays inside Blender.*

| Parameter | Type | Required | Default | Description |
| --- | --- | --- | --- | --- |
| `name` | `string` | yes | — | Name of the mesh object. |
| `type` | `string` | — | `EMITTER` | EMITTER or HAIR. |
| `count` | `integer?` | — | `null` | Number of particles or hairs. |
| `hairLength` | `number?` | — | `null` | HAIR: length in metres. |
| `frameStart` | `integer?` | — | `null` | EMITTER: first frame of emission. |
| `frameEnd` | `integer?` | — | `null` | EMITTER: last frame of emission. |
| `lifetime` | `integer?` | — | `null` | EMITTER: frames a particle lives. |
| `systemName` | `string?` | — | `null` | Name for the particle system. |
| `settings` | `object?` | — | `null` | Further particle settings by Python name, e.g. {"render_type": "OBJECT", "instance_object": "Leaf", "particle_size": 0.1, "normal_factor": 2}. |

<details><summary>JSON Schema</summary>

```json
{
  "type": "object",
  "properties": {
    "name": {
      "description": "Name of the mesh object.",
      "type": "string"
    },
    "type": {
      "description": "EMITTER or HAIR.",
      "type": "string",
      "default": "EMITTER"
    },
    "count": {
      "description": "Number of particles or hairs.",
      "type": [
        "integer",
        "null"
      ],
      "default": null
    },
    "hairLength": {
      "description": "HAIR: length in metres.",
      "type": [
        "number",
        "null"
      ],
      "default": null
    },
    "frameStart": {
      "description": "EMITTER: first frame of emission.",
      "type": [
        "integer",
        "null"
      ],
      "default": null
    },
    "frameEnd": {
      "description": "EMITTER: last frame of emission.",
      "type": [
        "integer",
        "null"
      ],
      "default": null
    },
    "lifetime": {
      "description": "EMITTER: frames a particle lives.",
      "type": [
        "integer",
        "null"
      ],
      "default": null
    },
    "systemName": {
      "description": "Name for the particle system.",
      "type": [
        "string",
        "null"
      ],
      "default": null
    },
    "settings": {
      "description": "Further particle settings by Python name, e.g. {\u0022render_type\u0022: \u0022OBJECT\u0022, \u0022instance_object\u0022: \u0022Leaf\u0022, \u0022particle_size\u0022: 0.1, \u0022normal_factor\u0022: 2}.",
      "type": [
        "object",
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

## <code>blender_<wbr>bake_<wbr>physics</code>

Bakes every physics cache of the scene up to a frame, or frees them; needed before rendering simulations.

*only adds · every call adds · stays inside Blender.*

| Parameter | Type | Required | Default | Description |
| --- | --- | --- | --- | --- |
| `frameEnd` | `integer?` | — | `null` | Last frame to simulate; the scene end otherwise. |
| `free` | `boolean` | — | `false` | Discard every cache instead of baking. |
| `timeoutSeconds` | `integer` | — | `600` | Seconds to wait for Blender, 1 to 600. |

<details><summary>JSON Schema</summary>

```json
{
  "type": "object",
  "properties": {
    "frameEnd": {
      "description": "Last frame to simulate; the scene end otherwise.",
      "type": [
        "integer",
        "null"
      ],
      "default": null
    },
    "free": {
      "description": "Discard every cache instead of baking.",
      "type": "boolean",
      "default": false
    },
    "timeoutSeconds": {
      "description": "Seconds to wait for Blender, 1 to 600.",
      "type": "integer",
      "default": 600
    }
  }
}
```

</details>

