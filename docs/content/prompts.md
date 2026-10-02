# Prompts

The server offers three MCP prompts. A client lists them with `prompts/list` and expands one with
`prompts/get`; the text puts the studio order of tools in front of the model before it starts.

| Prompt | Arguments | What it walks through |
|--------|-----------|-----------------------|
| `photoreal_product_shot` | `subject`, `output` (PNG by default) | World or HDRI, key, fill and rim lights, a physical camera, Cycles with adaptive sampling and OpenImageDenoise, AgX, a restrained post stack, a preview judged by its statistics, the pre-flight check, then the final render or job. |
| `farm_exr_delivery` | `frames`, `directory` | Pre-flight check, view layer passes, cryptomatte and light groups, multi-layer EXR at 32 bit with ZIP, denoising data for the compositor, a memory budget with farm packing, a chunked background job. |
| `vse_grade_and_deliver` | `sources`, `delivery` (`H264 review` by default) | Timing, strips and cuts, proxies, grading with strip modifiers, ProRes or H.264 encode settings, the encode and the audio mixdown. |

Every tool a prompt names exists: a guard test checks the texts against the tool catalog.
