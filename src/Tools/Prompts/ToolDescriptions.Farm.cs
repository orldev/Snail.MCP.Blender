namespace Snail.MCP.Blender.Tools.Prompts;

public static partial class ToolDescriptions
{
    /// <summary><c>JobTools</c>.</summary>
    public static class Jobs
    {
        /// <summary><c>blender_render_job</c></summary>
        public const string Start =
            "Starts a background render: the file is saved as a copy and a second Blender renders it, so this Blender and " +
            "the tools stay free. Frames, cameras, view layers, engine, output template with {scene} and {camera} and " +
            "####, format and depth. Jobs queue up and run maxParallel at a time, retry on failure, and can split into " +
            "chunks that render interleaved frames in parallel. Returns the job id; poll blender_render_job_status. Save " +
            "the file first: unsaved changes are included, but relative paths resolve best from a saved file.";

        /// <summary><c>blender_render_job_status</c></summary>
        public const string Status =
            "Progress of a background render: state, frames done of total, current frame and camera, elapsed and " +
            "estimated remaining seconds, peak memory, the files written, the error if it failed and the tail of its log. " +
            "Without an id, lists recent jobs.";

        /// <summary><c>blender_render_job_cancel</c></summary>
        public const string Cancel = "Stops a background render; frames already written stay on disk.";

        /// <summary><c>blender_render_check</c></summary>
        public const string Check =
            "Pre-flight before a long render: camera, writable output and free disk, unsaved file or changes, missing " +
            "textures and libraries, disabled view layers, frame range, GPU availability, world and lights, codec. " +
            "Returns ready true or false with the issues by level.";
    }

    /// <summary><c>BudgetTools</c>.</summary>
    public static class Budget
    {
        /// <summary><c>blender_render_budget</c></summary>
        public const string Estimate =
            "What the frame will cost before rendering it: texture memory by size and depth, geometry after modifiers, " +
            "instances, particles and hair, the Cycles devices and system memory, and a probe that renders the frame twice at low " +
            "resolution — the pair separates the fixed cost of a frame from the cost that grows with its pixels, and the estimate is " +
            "their sum for the whole frame. The memory estimate says where it came from: the probe's own peak plus the frame buffer it " +
            "did not fill, or the textures and geometry when no probe ran. Verdict fits, tight or exceeds against a memory limit, with " +
            "recommendations that can be applied in the same call: texture limit, simplify, tile size, device. Optionally packs external " +
            "files and saves a copy for a farm.";
    }
}
