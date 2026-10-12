# Background render jobs and the pre-flight check

Part of the `farm` skill: these tools appear once it is loaded — by `blender_enable_skill` with `farm`, or by `find_tool` when a search returns one of them.

## <code>blender_<wbr>render_<wbr>job</code>

Starts a background render: the file is saved as a copy and a second Blender renders it, so this Blender and the tools stay free. Frames, cameras, view layers, engine, output template with {scene} and {camera} and ####, format and depth. Jobs queue up and run maxParallel at a time, retry on failure, and can split into chunks that render interleaved frames in parallel. Returns the job id; poll blender_render_job_status. Save the file first: unsaved changes are included, but relative paths resolve best from a saved file.

*only adds · every call adds · stays inside Blender.*

| Parameter | Type | Required | Default | Description |
| --- | --- | --- | --- | --- |
| `outputPath` | `string` | yes | — | Output path template: folder and prefix with #### for the frame number, {scene} and {camera} for their names, e.g. /renders/{scene}/{camera}_####. Blender adds the extension; a finished file name such as shot_0001.png is refused, pass shot_####.png. |
| `frames` | `string?` | — | `null` | Frames to render: 1-240, every second frame 1-240x2, a list 1,5,9-12; the scene range otherwise. |
| `cameras` | `array?` | — | `null` | Cameras to render one after another, each into its own files through {camera}; the scene camera otherwise. |
| `viewLayers` | `array?` | — | `null` | View layers to render; every enabled layer otherwise. |
| `engine` | `string?` | — | `null` | EEVEE, CYCLES or WORKBENCH; the scene's engine otherwise. |
| `output` | `object?` | — | `null` | What the job writes: format, depth, codec, size, samples, overwrite and placeholders. |
| `queue` | `object?` | — | `null` | How the job runs: chunks, parallel jobs, retries, priority, threads and OCIO config. |
| `scene` | `string?` | — | `null` | Scene to act on; the current scene otherwise. |

<details><summary>JSON Schema</summary>

```json
{
  "type": "object",
  "properties": {
    "outputPath": {
      "description": "Output path template: folder and prefix with #### for the frame number, {scene} and {camera} for their names, e.g. /renders/{scene}/{camera}_####. Blender adds the extension; a finished file name such as shot_0001.png is refused, pass shot_####.png.",
      "type": "string"
    },
    "frames": {
      "description": "Frames to render: 1-240, every second frame 1-240x2, a list 1,5,9-12; the scene range otherwise.",
      "type": [
        "string",
        "null"
      ],
      "default": null
    },
    "cameras": {
      "description": "Cameras to render one after another, each into its own files through {camera}; the scene camera otherwise.",
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
    "viewLayers": {
      "description": "View layers to render; every enabled layer otherwise.",
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
    "engine": {
      "description": "EEVEE, CYCLES or WORKBENCH; the scene\u0027s engine otherwise.",
      "type": [
        "string",
        "null"
      ],
      "default": null
    },
    "output": {
      "description": "What the job writes: format, depth, codec, size, samples, overwrite and placeholders.",
      "type": [
        "object",
        "null"
      ],
      "properties": {
        "fileFormat": {
          "description": "PNG, JPEG, OPEN_EXR, OPEN_EXR_MULTILAYER, TIFF, WEBP or FFMPEG; the scene\u0027s format otherwise.",
          "type": [
            "string",
            "null"
          ]
        },
        "colorDepth": {
          "description": "Bits per channel: 8 or 16 for PNG and TIFF, 16 (half) or 32 (full float) for EXR, 8, 10 or 12 for video and DPX.",
          "type": [
            "integer",
            "null"
          ]
        },
        "exrCodec": {
          "description": "EXR compression: ZIP (lossless, the default), PIZ, ZIPS, RLE, PXR24 (lossy for 32-bit), DWAA and DWAB (lossy, small), B44, B44A, HTJ2K or NONE.",
          "type": [
            "string",
            "null"
          ]
        },
        "colorMode": {
          "description": "BW, RGB or RGBA.",
          "type": [
            "string",
            "null"
          ]
        },
        "resolutionX": {
          "description": "Width in pixels, up to 4096.",
          "type": [
            "integer",
            "null"
          ]
        },
        "resolutionY": {
          "description": "Height in pixels, up to 4096.",
          "type": [
            "integer",
            "null"
          ]
        },
        "percentage": {
          "description": "Resolution percentage 1 to 100.",
          "type": [
            "integer",
            "null"
          ]
        },
        "samples": {
          "description": "Samples per pixel for this job only.",
          "type": [
            "integer",
            "null"
          ]
        },
        "overwrite": {
          "description": "Overwrite frames that exist; false skips them.",
          "type": [
            "boolean",
            "null"
          ]
        },
        "placeholder": {
          "description": "Write placeholders so several machines can share the range.",
          "type": [
            "boolean",
            "null"
          ]
        }
      },
      "default": null
    },
    "queue": {
      "description": "How the job runs: chunks, parallel jobs, retries, priority, threads and OCIO config.",
      "type": [
        "object",
        "null"
      ],
      "properties": {
        "chunks": {
          "description": "Processes to split the frame range over; each renders every nth frame, so the sequence fills in evenly. 1 keeps one process.",
          "type": [
            "integer",
            "null"
          ]
        },
        "maxParallel": {
          "description": "Jobs allowed to run at once on this machine; the rest wait in the queue. 1 keeps one GPU to one render.",
          "type": [
            "integer",
            "null"
          ]
        },
        "retries": {
          "description": "Restarts after a crash before the job counts as failed.",
          "type": [
            "integer",
            "null"
          ]
        },
        "priority": {
          "description": "Queue priority; higher starts first.",
          "type": [
            "integer",
            "null"
          ]
        },
        "threads": {
          "description": "CPU threads for the job; 0 uses every core.",
          "type": [
            "integer",
            "null"
          ]
        },
        "ocioConfig": {
          "description": "OpenColorIO config file the job renders under; the server\u0027s configured one otherwise, else Blender\u0027s own.",
          "type": [
            "string",
            "null"
          ]
        }
      },
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
    "outputPath"
  ]
}
```

</details>

## <code>blender_<wbr>render_<wbr>job_<wbr>status</code>

Progress of a background render: state, frames done of total, current frame and camera, elapsed and estimated remaining seconds, peak memory, the files written, the error if it failed and the tail of its log. Without an id, lists recent jobs.

*read-only · every call adds · stays inside Blender.*

| Parameter | Type | Required | Default | Description |
| --- | --- | --- | --- | --- |
| `id` | `string?` | — | `null` | Job id as blender_render_job returned it. |
| `logLines` | `integer` | — | `20` | Lines from the end of the job's log to include. |

<details><summary>JSON Schema</summary>

```json
{
  "type": "object",
  "properties": {
    "id": {
      "description": "Job id as blender_render_job returned it.",
      "type": [
        "string",
        "null"
      ],
      "default": null
    },
    "logLines": {
      "description": "Lines from the end of the job\u0027s log to include.",
      "type": "integer",
      "default": 20
    }
  }
}
```

</details>

## <code>blender_<wbr>render_<wbr>job_<wbr>cancel</code>

Stops a background render; frames already written stay on disk.

*may overwrite or delete · every call adds · stays inside Blender.*

| Parameter | Type | Required | Default | Description |
| --- | --- | --- | --- | --- |
| `id` | `string` | yes | — | Job id as blender_render_job returned it. |

<details><summary>JSON Schema</summary>

```json
{
  "type": "object",
  "properties": {
    "id": {
      "description": "Job id as blender_render_job returned it.",
      "type": "string"
    }
  },
  "required": [
    "id"
  ]
}
```

</details>

## <code>blender_<wbr>render_<wbr>check</code>

Pre-flight before a long render: camera, writable output and free disk, unsaved file or changes, missing textures and libraries, disabled view layers, frame range, GPU availability, world and lights, codec. Returns ready true or false with the issues by level.

*read-only · every call adds · stays inside Blender.*

| Parameter | Type | Required | Default | Description |
| --- | --- | --- | --- | --- |
| `outputPath` | `string?` | — | `null` | Output path to check instead of the scene's; a template with #### works. |
| `scene` | `string?` | — | `null` | Scene to act on; the current scene otherwise. |

<details><summary>JSON Schema</summary>

```json
{
  "type": "object",
  "properties": {
    "outputPath": {
      "description": "Output path to check instead of the scene\u0027s; a template with #### works.",
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

