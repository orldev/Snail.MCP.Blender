# Cycles, EEVEE, motion blur and Freestyle

Part of the `rendering` skill: these tools appear once it is loaded — by `blender_enable_skill` with `rendering`, or by `find_tool` when a search returns one of them.

## <code>blender_<wbr>set_<wbr>cycles</code>

Switches to Cycles and sets its sampling in depth: samples, adaptive sampling with threshold and minimum, a time limit, the denoiser with prefilter, passes and quality, GPU denoising, device and compute backend (CUDA, OptiX, HIP, oneAPI, Metal) with the devices this machine has, light paths per bounce type, caustics, clamping, light tree, seed and persistent data; anything else by Python name.

*only adds · every call adds · stays inside Blender.*

| Parameter | Type | Required | Default | Description |
| --- | --- | --- | --- | --- |
| `activate` | `boolean` | — | `true` | Switch the scene to Cycles; false only changes the settings. |
| `samples` | `integer?` | — | `null` | Maximum samples per pixel; 64 to 256 for previews, 1024 to 4096 for finals with adaptive sampling. |
| `adaptiveSampling` | `boolean?` | — | `null` | Adaptive sampling on or off; it stops sampling pixels that converged. |
| `adaptiveThreshold` | `number?` | — | `null` | Noise threshold for adaptive sampling; 0.01 is the default, 0.001 cleaner and slower. |
| `adaptiveMinSamples` | `integer?` | — | `null` | Minimum samples before adaptive sampling may stop; 0 lets Blender choose. |
| `timeLimit` | `number?` | — | `null` | Seconds per frame after which sampling stops; 0 is no limit. |
| `denoise` | `boolean?` | — | `null` | Denoise the final render. |
| `denoiser` | `string?` | — | `null` | OPENIMAGEDENOISE (every platform) or OPTIX (NVIDIA only); the reply lists what this machine has. |
| `denoisePrefilter` | `string?` | — | `null` | Denoiser prefilter: ACCURATE (the default), FAST or NONE. |
| `denoisePasses` | `string?` | — | `null` | Passes the denoiser uses: RGB, RGB_ALBEDO or RGB_ALBEDO_NORMAL (the default, best). |
| `denoiseQuality` | `string?` | — | `null` | Denoiser quality: HIGH, BALANCED or FAST. |
| `denoiseGpu` | `boolean?` | — | `null` | Run the denoiser on the GPU. |
| `device` | `string?` | — | `null` | CPU or GPU; GPU needs a compute backend, which backend sets. |
| `backend` | `string?` | — | `null` | Compute backend for GPU rendering: OPTIX or CUDA (NVIDIA), HIP (AMD), ONEAPI (Intel), METAL (Apple), NONE for the CPU alone. Enables every device of that backend and sets the device to GPU; the reply lists the devices this machine has, and render jobs take the choice to their worker. |
| `bounces` | `object?` | — | `null` | Light path bounces per type. |
| `caustics` | `object?` | — | `null` | Caustics; off with glossy blur reduces fireflies. |
| `clamp` | `object?` | — | `null` | Clamp sample brightness; 0 is no clamp. |
| `lightTree` | `boolean?` | — | `null` | Light tree for many-light scenes. |
| `seed` | `integer?` | — | `null` | Noise seed; animatedSeed changes it per frame. |
| `animatedSeed` | `boolean?` | — | `null` | Change the seed every frame so noise does not stick in animations. |
| `persistentData` | `boolean?` | — | `null` | Keep scene data between frames; faster animations, more memory. |
| `settings` | `object?` | — | `null` | Any other scene.cycles setting by Python name, e.g. {"use_guiding": true, "volume_step_rate": 0.5, "texture_limit_render": "2048"}. |
| `scene` | `string?` | — | `null` | Scene to act on; the current scene otherwise. |

<details><summary>JSON Schema</summary>

```json
{
  "type": "object",
  "properties": {
    "activate": {
      "description": "Switch the scene to Cycles; false only changes the settings.",
      "type": "boolean",
      "default": true
    },
    "samples": {
      "description": "Maximum samples per pixel; 64 to 256 for previews, 1024 to 4096 for finals with adaptive sampling.",
      "type": [
        "integer",
        "null"
      ],
      "default": null
    },
    "adaptiveSampling": {
      "description": "Adaptive sampling on or off; it stops sampling pixels that converged.",
      "type": [
        "boolean",
        "null"
      ],
      "default": null
    },
    "adaptiveThreshold": {
      "description": "Noise threshold for adaptive sampling; 0.01 is the default, 0.001 cleaner and slower.",
      "type": [
        "number",
        "null"
      ],
      "default": null
    },
    "adaptiveMinSamples": {
      "description": "Minimum samples before adaptive sampling may stop; 0 lets Blender choose.",
      "type": [
        "integer",
        "null"
      ],
      "default": null
    },
    "timeLimit": {
      "description": "Seconds per frame after which sampling stops; 0 is no limit.",
      "type": [
        "number",
        "null"
      ],
      "default": null
    },
    "denoise": {
      "description": "Denoise the final render.",
      "type": [
        "boolean",
        "null"
      ],
      "default": null
    },
    "denoiser": {
      "description": "OPENIMAGEDENOISE (every platform) or OPTIX (NVIDIA only); the reply lists what this machine has.",
      "type": [
        "string",
        "null"
      ],
      "default": null
    },
    "denoisePrefilter": {
      "description": "Denoiser prefilter: ACCURATE (the default), FAST or NONE.",
      "type": [
        "string",
        "null"
      ],
      "default": null
    },
    "denoisePasses": {
      "description": "Passes the denoiser uses: RGB, RGB_ALBEDO or RGB_ALBEDO_NORMAL (the default, best).",
      "type": [
        "string",
        "null"
      ],
      "default": null
    },
    "denoiseQuality": {
      "description": "Denoiser quality: HIGH, BALANCED or FAST.",
      "type": [
        "string",
        "null"
      ],
      "default": null
    },
    "denoiseGpu": {
      "description": "Run the denoiser on the GPU.",
      "type": [
        "boolean",
        "null"
      ],
      "default": null
    },
    "device": {
      "description": "CPU or GPU; GPU needs a compute backend, which backend sets.",
      "type": [
        "string",
        "null"
      ],
      "default": null
    },
    "backend": {
      "description": "Compute backend for GPU rendering: OPTIX or CUDA (NVIDIA), HIP (AMD), ONEAPI (Intel), METAL (Apple), NONE for the CPU alone. Enables every device of that backend and sets the device to GPU; the reply lists the devices this machine has, and render jobs take the choice to their worker.",
      "type": [
        "string",
        "null"
      ],
      "default": null
    },
    "bounces": {
      "description": "Light path bounces per type.",
      "type": [
        "object",
        "null"
      ],
      "properties": {
        "total": {
          "description": "Total bounces; 12 is the default.",
          "type": [
            "integer",
            "null"
          ]
        },
        "diffuse": {
          "type": [
            "integer",
            "null"
          ]
        },
        "glossy": {
          "type": [
            "integer",
            "null"
          ]
        },
        "transmission": {
          "type": [
            "integer",
            "null"
          ]
        },
        "volume": {
          "type": [
            "integer",
            "null"
          ]
        },
        "transparent": {
          "description": "Transparent bounces; raise for leaves and hair with alpha.",
          "type": [
            "integer",
            "null"
          ]
        }
      },
      "default": null
    },
    "caustics": {
      "description": "Caustics; off with glossy blur reduces fireflies.",
      "type": [
        "object",
        "null"
      ],
      "properties": {
        "reflective": {
          "type": [
            "boolean",
            "null"
          ]
        },
        "refractive": {
          "type": [
            "boolean",
            "null"
          ]
        },
        "blurGlossy": {
          "description": "Blur glossy reflections after blurry bounces to kill fireflies; 1 is the default.",
          "type": [
            "number",
            "null"
          ]
        }
      },
      "default": null
    },
    "clamp": {
      "description": "Clamp sample brightness; 0 is no clamp.",
      "type": [
        "object",
        "null"
      ],
      "properties": {
        "direct": {
          "description": "Clamp direct light; 0 is none.",
          "type": [
            "number",
            "null"
          ]
        },
        "indirect": {
          "description": "Clamp indirect light; 10 removes fireflies with little energy loss.",
          "type": [
            "number",
            "null"
          ]
        }
      },
      "default": null
    },
    "lightTree": {
      "description": "Light tree for many-light scenes.",
      "type": [
        "boolean",
        "null"
      ],
      "default": null
    },
    "seed": {
      "description": "Noise seed; animatedSeed changes it per frame.",
      "type": [
        "integer",
        "null"
      ],
      "default": null
    },
    "animatedSeed": {
      "description": "Change the seed every frame so noise does not stick in animations.",
      "type": [
        "boolean",
        "null"
      ],
      "default": null
    },
    "persistentData": {
      "description": "Keep scene data between frames; faster animations, more memory.",
      "type": [
        "boolean",
        "null"
      ],
      "default": null
    },
    "settings": {
      "description": "Any other scene.cycles setting by Python name, e.g. {\u0022use_guiding\u0022: true, \u0022volume_step_rate\u0022: 0.5, \u0022texture_limit_render\u0022: \u00222048\u0022}.",
      "type": [
        "object",
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

## <code>blender_<wbr>set_<wbr>eevee</code>

Switches to EEVEE and sets its quality: samples, shadows with rays, steps and resolution, ray tracing with its options, fast GI, volumetrics, clamping, depth-of-field bokeh, overscan and high quality normals; anything else by Python name.

*only adds · every call adds · stays inside Blender.*

| Parameter | Type | Required | Default | Description |
| --- | --- | --- | --- | --- |
| `activate` | `boolean` | — | `true` | Switch the scene to EEVEE; false only changes the settings. |
| `samples` | `integer?` | — | `null` | Render samples; 64 is the default, 16 for previews, 256 and up for clean soft shadows. |
| `viewportSamples` | `integer?` | — | `null` | Viewport samples. |
| `shadows` | `boolean?` | — | `null` | Shadows on or off. |
| `shadowRays` | `integer?` | — | `null` | Shadow rays per sample, 1 to 16; more is smoother. |
| `shadowSteps` | `integer?` | — | `null` | Shadow steps per ray, 1 to 16. |
| `shadowResolution` | `number?` | — | `null` | Shadow map resolution scale, 0.25 to 4. |
| `raytracing` | `boolean?` | — | `null` | Screen-space ray tracing for reflections and refractions. |
| `raytracingOptions` | `object?` | — | `null` | Ray tracing options. |
| `fastGi` | `boolean?` | — | `null` | Fast global illumination approximation for rough surfaces. |
| `fastGiOptions` | `object?` | — | `null` | Fast GI options. |
| `volumetrics` | `object?` | — | `null` | Volumetrics quality and range. |
| `clamp` | `object?` | — | `null` | Clamp light per surface and volume. |
| `bokeh` | `object?` | — | `null` | Depth of field bokeh. |
| `overscan` | `boolean?` | — | `null` | Render a margin outside the frame so screen-space effects do not fade at the edges. |
| `overscanSize` | `number?` | — | `null` | Overscan size in percent of the frame, 0 to 50. |
| `highQualityNormals` | `boolean?` | — | `null` | High quality normals for smooth shading. |
| `settings` | `object?` | — | `null` | Any other scene.eevee setting by Python name, e.g. {"use_taa_reprojection": true, "shadow_pool_size": "1024"}. |
| `scene` | `string?` | — | `null` | Scene to act on; the current scene otherwise. |

<details><summary>JSON Schema</summary>

```json
{
  "type": "object",
  "properties": {
    "activate": {
      "description": "Switch the scene to EEVEE; false only changes the settings.",
      "type": "boolean",
      "default": true
    },
    "samples": {
      "description": "Render samples; 64 is the default, 16 for previews, 256 and up for clean soft shadows.",
      "type": [
        "integer",
        "null"
      ],
      "default": null
    },
    "viewportSamples": {
      "description": "Viewport samples.",
      "type": [
        "integer",
        "null"
      ],
      "default": null
    },
    "shadows": {
      "description": "Shadows on or off.",
      "type": [
        "boolean",
        "null"
      ],
      "default": null
    },
    "shadowRays": {
      "description": "Shadow rays per sample, 1 to 16; more is smoother.",
      "type": [
        "integer",
        "null"
      ],
      "default": null
    },
    "shadowSteps": {
      "description": "Shadow steps per ray, 1 to 16.",
      "type": [
        "integer",
        "null"
      ],
      "default": null
    },
    "shadowResolution": {
      "description": "Shadow map resolution scale, 0.25 to 4.",
      "type": [
        "number",
        "null"
      ],
      "default": null
    },
    "raytracing": {
      "description": "Screen-space ray tracing for reflections and refractions.",
      "type": [
        "boolean",
        "null"
      ],
      "default": null
    },
    "raytracingOptions": {
      "description": "Ray tracing options.",
      "type": [
        "object",
        "null"
      ],
      "properties": {
        "resolution": {
          "description": "Trace resolution: \u00221\u0022, \u00222\u0022 or \u00224\u0022 (divisor).",
          "type": [
            "string",
            "null"
          ]
        },
        "maxRoughness": {
          "description": "Roughness above which reflections fall back to probes.",
          "type": [
            "number",
            "null"
          ]
        },
        "quality": {
          "description": "Trace precision 0 to 1.",
          "type": [
            "number",
            "null"
          ]
        },
        "thickness": {
          "description": "Surface thickness assumed by the screen trace.",
          "type": [
            "number",
            "null"
          ]
        },
        "denoise": {
          "type": [
            "boolean",
            "null"
          ]
        },
        "denoiseSpatial": {
          "type": [
            "boolean",
            "null"
          ]
        },
        "denoiseTemporal": {
          "type": [
            "boolean",
            "null"
          ]
        },
        "denoiseBilateral": {
          "type": [
            "boolean",
            "null"
          ]
        }
      },
      "default": null
    },
    "fastGi": {
      "description": "Fast global illumination approximation for rough surfaces.",
      "type": [
        "boolean",
        "null"
      ],
      "default": null
    },
    "fastGiOptions": {
      "description": "Fast GI options.",
      "type": [
        "object",
        "null"
      ],
      "properties": {
        "method": {
          "description": "AMBIENT_OCCLUSION_ONLY or GLOBAL_ILLUMINATION.",
          "type": [
            "string",
            "null"
          ]
        },
        "quality": {
          "type": [
            "number",
            "null"
          ]
        },
        "rays": {
          "type": [
            "integer",
            "null"
          ]
        },
        "steps": {
          "type": [
            "integer",
            "null"
          ]
        },
        "distance": {
          "description": "Distance in metres; 0 is unlimited.",
          "type": [
            "number",
            "null"
          ]
        },
        "resolution": {
          "description": "\u00221\u0022, \u00222\u0022 or \u00224\u0022.",
          "type": [
            "string",
            "null"
          ]
        },
        "bias": {
          "type": [
            "number",
            "null"
          ]
        },
        "thickness": {
          "type": [
            "number",
            "null"
          ]
        }
      },
      "default": null
    },
    "volumetrics": {
      "description": "Volumetrics quality and range.",
      "type": [
        "object",
        "null"
      ],
      "properties": {
        "tileSize": {
          "description": "\u00222\u0022, \u00224\u0022, \u00228\u0022 or \u002216\u0022; smaller is finer and slower.",
          "type": [
            "string",
            "null"
          ]
        },
        "samples": {
          "type": [
            "integer",
            "null"
          ]
        },
        "distribution": {
          "description": "Sample distribution 0 to 1; higher packs samples near the camera.",
          "type": [
            "number",
            "null"
          ]
        },
        "start": {
          "type": [
            "number",
            "null"
          ]
        },
        "end": {
          "type": [
            "number",
            "null"
          ]
        },
        "customRange": {
          "type": [
            "boolean",
            "null"
          ]
        },
        "shadows": {
          "type": [
            "boolean",
            "null"
          ]
        },
        "shadowSamples": {
          "type": [
            "integer",
            "null"
          ]
        },
        "rayDepth": {
          "type": [
            "integer",
            "null"
          ]
        },
        "lightClamp": {
          "type": [
            "number",
            "null"
          ]
        }
      },
      "default": null
    },
    "clamp": {
      "description": "Clamp light per surface and volume.",
      "type": [
        "object",
        "null"
      ],
      "properties": {
        "surfaceDirect": {
          "type": [
            "number",
            "null"
          ]
        },
        "surfaceIndirect": {
          "type": [
            "number",
            "null"
          ]
        },
        "volumeDirect": {
          "type": [
            "number",
            "null"
          ]
        },
        "volumeIndirect": {
          "type": [
            "number",
            "null"
          ]
        }
      },
      "default": null
    },
    "bokeh": {
      "description": "Depth of field bokeh.",
      "type": [
        "object",
        "null"
      ],
      "properties": {
        "maxSize": {
          "description": "Largest bokeh in pixels.",
          "type": [
            "number",
            "null"
          ]
        },
        "threshold": {
          "type": [
            "number",
            "null"
          ]
        },
        "neighborMax": {
          "type": [
            "number",
            "null"
          ]
        },
        "jittered": {
          "type": [
            "boolean",
            "null"
          ]
        },
        "overblur": {
          "type": [
            "number",
            "null"
          ]
        }
      },
      "default": null
    },
    "overscan": {
      "description": "Render a margin outside the frame so screen-space effects do not fade at the edges.",
      "type": [
        "boolean",
        "null"
      ],
      "default": null
    },
    "overscanSize": {
      "description": "Overscan size in percent of the frame, 0 to 50.",
      "type": [
        "number",
        "null"
      ],
      "default": null
    },
    "highQualityNormals": {
      "description": "High quality normals for smooth shading.",
      "type": [
        "boolean",
        "null"
      ],
      "default": null
    },
    "settings": {
      "description": "Any other scene.eevee setting by Python name, e.g. {\u0022use_taa_reprojection\u0022: true, \u0022shadow_pool_size\u0022: \u00221024\u0022}.",
      "type": [
        "object",
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

## <code>blender_<wbr>set_<wbr>motion_<wbr>blur</code>

Sets motion blur for renders: on or off, shutter time in frames, shutter position, Cycles rolling shutter, EEVEE steps and maximum blur, and whether the view layers use it.

*only adds · every call adds · stays inside Blender.*

| Parameter | Type | Required | Default | Description |
| --- | --- | --- | --- | --- |
| `enabled` | `boolean?` | — | `null` | Motion blur on or off. |
| `shutter` | `number?` | — | `null` | Shutter time in frames; 0.5 is a 180° shutter, the film look. |
| `position` | `string?` | — | `null` | Where the shutter opens relative to the frame: START, CENTER (the default) or END. |
| `rollingShutter` | `boolean?` | — | `null` | Cycles rolling shutter: true (TOP), false (NONE). |
| `rollingShutterDuration` | `number?` | — | `null` | Cycles rolling shutter duration as a fraction of the frame, 0 to 1. |
| `steps` | `integer?` | — | `null` | EEVEE motion steps, 1 and up; more steps blur curved motion correctly. |
| `maxBlur` | `integer?` | — | `null` | EEVEE maximum blur in pixels. |
| `depthScale` | `number?` | — | `null` | EEVEE depth scale that separates foreground from background blur. |
| `viewLayerEnabled` | `boolean?` | — | `null` | Apply motion blur on every view layer of the scene, or not. |
| `shutterCurve` | `array?` | — | `null` | Shutter opening over the exposure as [[t, open], ...] from t 0 to 1; [[0,0],[0.1,1],[0.9,1],[1,0]] imitates a mechanical shutter, a flat 1 is the default. |
| `scene` | `string?` | — | `null` | Scene to act on; the current scene otherwise. |

<details><summary>JSON Schema</summary>

```json
{
  "type": "object",
  "properties": {
    "enabled": {
      "description": "Motion blur on or off.",
      "type": [
        "boolean",
        "null"
      ],
      "default": null
    },
    "shutter": {
      "description": "Shutter time in frames; 0.5 is a 180\u00B0 shutter, the film look.",
      "type": [
        "number",
        "null"
      ],
      "default": null
    },
    "position": {
      "description": "Where the shutter opens relative to the frame: START, CENTER (the default) or END.",
      "type": [
        "string",
        "null"
      ],
      "default": null
    },
    "rollingShutter": {
      "description": "Cycles rolling shutter: true (TOP), false (NONE).",
      "type": [
        "boolean",
        "null"
      ],
      "default": null
    },
    "rollingShutterDuration": {
      "description": "Cycles rolling shutter duration as a fraction of the frame, 0 to 1.",
      "type": [
        "number",
        "null"
      ],
      "default": null
    },
    "steps": {
      "description": "EEVEE motion steps, 1 and up; more steps blur curved motion correctly.",
      "type": [
        "integer",
        "null"
      ],
      "default": null
    },
    "maxBlur": {
      "description": "EEVEE maximum blur in pixels.",
      "type": [
        "integer",
        "null"
      ],
      "default": null
    },
    "depthScale": {
      "description": "EEVEE depth scale that separates foreground from background blur.",
      "type": [
        "number",
        "null"
      ],
      "default": null
    },
    "viewLayerEnabled": {
      "description": "Apply motion blur on every view layer of the scene, or not.",
      "type": [
        "boolean",
        "null"
      ],
      "default": null
    },
    "shutterCurve": {
      "description": "Shutter opening over the exposure as [[t, open], ...] from t 0 to 1; [[0,0],[0.1,1],[0.9,1],[1,0]] imitates a mechanical shutter, a flat 1 is the default.",
      "type": [
        "array",
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

## <code>blender_<wbr>set_<wbr>freestyle</code>

Freestyle line rendering: on or off, line thickness and mode, crease angle, and line sets with their edge types, visibility, colour and thickness; for toon, technical and ink looks.

*only adds · every call adds · stays inside Blender.*

| Parameter | Type | Required | Default | Description |
| --- | --- | --- | --- | --- |
| `enabled` | `boolean?` | — | `null` | Freestyle on or off for the scene and view layer. |
| `lineThickness` | `number?` | — | `null` | Base line thickness in pixels. |
| `thicknessMode` | `string?` | — | `null` | ABSOLUTE or RELATIVE to the resolution. |
| `creaseAngle` | `number?` | — | `null` | Angle in degrees above which an edge counts as a crease. |
| `culling` | `boolean?` | — | `null` | Cull lines outside the view. |
| `linesets` | `array?` | — | `null` | Line sets to create or update. |
| `removeLinesets` | `array?` | — | `null` | Line set names to remove. |
| `viewLayer` | `string?` | — | `null` | View layer; the active one otherwise. |
| `scene` | `string?` | — | `null` | Scene to act on; the current scene otherwise. |

<details><summary>JSON Schema</summary>

```json
{
  "type": "object",
  "properties": {
    "enabled": {
      "description": "Freestyle on or off for the scene and view layer.",
      "type": [
        "boolean",
        "null"
      ],
      "default": null
    },
    "lineThickness": {
      "description": "Base line thickness in pixels.",
      "type": [
        "number",
        "null"
      ],
      "default": null
    },
    "thicknessMode": {
      "description": "ABSOLUTE or RELATIVE to the resolution.",
      "type": [
        "string",
        "null"
      ],
      "default": null
    },
    "creaseAngle": {
      "description": "Angle in degrees above which an edge counts as a crease.",
      "type": [
        "number",
        "null"
      ],
      "default": null
    },
    "culling": {
      "description": "Cull lines outside the view.",
      "type": [
        "boolean",
        "null"
      ],
      "default": null
    },
    "linesets": {
      "description": "Line sets to create or update.",
      "type": [
        "array",
        "null"
      ],
      "items": {
        "type": [
          "object",
          "null"
        ],
        "properties": {
          "name": {
            "description": "Line set name; LineSet is Blender\u0027s default one.",
            "type": [
              "string",
              "null"
            ]
          },
          "enabled": {
            "type": [
              "boolean",
              "null"
            ]
          },
          "edges": {
            "description": "Edge types that draw: silhouette, border, crease, contour, external_contour, material_boundary, edge_mark, suggestive_contour, ridge_valley.",
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
          "visibility": {
            "description": "VISIBLE, HIDDEN or RANGE.",
            "type": [
              "string",
              "null"
            ]
          },
          "color": {
            "description": "Line colour [r, g, b].",
            "type": [
              "array",
              "null"
            ],
            "items": {
              "type": "number"
            }
          },
          "thickness": {
            "description": "Line thickness in pixels.",
            "type": [
              "number",
              "null"
            ]
          },
          "alpha": {
            "type": [
              "number",
              "null"
            ]
          }
        }
      },
      "default": null
    },
    "removeLinesets": {
      "description": "Line set names to remove.",
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
    "viewLayer": {
      "description": "View layer; the active one otherwise.",
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
  }
}
```

</details>

