using Snail.MCP.Blender.Application.Access;
using Snail.MCP.Blender.Configuration;
using Snail.MCP.Blender.Tools.Prompts;

namespace Snail.MCP.Blender.Web.Pages;

/// <summary>What the pages show, read out of the add-on's and the server's own reports.</summary>
public static class PageViews
{
    public static IReadOnlyList<Section> Sections { get; } =
    [
        new("Status", "/"),
        new("Files", "/files"),
        new("Jobs", "/jobs"),
        new("Batches", "/batches"),
        new("Snapshots", "/snapshots"),
    ];

    private static readonly HashSet<string> Images = new(StringComparer.OrdinalIgnoreCase) { ".png", ".jpg", ".jpeg", ".webp", ".gif", ".bmp", ".svg" };

    private static readonly HashSet<string> Texts = new(StringComparer.OrdinalIgnoreCase)
    {
        ".txt", ".log", ".json", ".py", ".md", ".csv", ".toml", ".xml", ".yaml", ".yml", ".ini", ".cfg", ".mtl", ".usda", ".osl", ".glsl",
    };

    public const long LargestTextPreview = 256 * 1024;

    public static string TitleOf(string area) => Sections.First(section => section.Href == $"/{area}").Label;

    public static string Page(string area, string path) => path.Length == 0 ? $"/{area}" : $"/{area}/{Escape(path)}";

    public static string Raw(string area, string path) => $"{FileLinks.RawPrefix}/{area}/{Escape(path)}";

    public static string Join(string folder, string name) => folder.Length == 0 ? name : $"{folder}/{name}";

    /// <summary>Each segment escaped on its own, so a folder named with spaces or in Cyrillic survives the address and slashes stay separators.</summary>
    private static string Escape(string path) => string.Join('/', path.Split('/').Select(Uri.EscapeDataString));

    public static bool IsImage(string name) => Images.Contains(Path.GetExtension(name));

    public static bool IsText(string name) => Texts.Contains(Path.GetExtension(name));

    public static IReadOnlyList<Crumb> Crumbs(string area, string path)
    {
        var crumbs = new List<Crumb> { new(area, Page(area, string.Empty)) };
        var walked = string.Empty;

        foreach (var segment in path.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            walked = Join(walked, segment);
            crumbs.Add(new Crumb(segment, Page(area, walked)));
        }

        return crumbs;
    }

    public static BrowseView Browse(string area, string path, JsonObject listing, Flash? flash)
    {
        var directory = listing["directory"]?.GetValue<string>() ?? string.Empty;
        var open = listing["open_file"]?.GetValue<string>();
        var entries = (listing["entries"] as JsonArray ?? [])
            .OfType<JsonObject>()
            .Select(entry => Entry(area, path, directory, open, entry))
            .ToList();

        return new BrowseView
        {
            Area = area,
            Title = TitleOf(area),
            Path = path,
            Crumbs = Crumbs(area, path),
            Entries = entries,
            Truncated = listing["truncated"]?.GetValue<bool>() == true,
            ZipHref = area == "snapshots" || entries.Count == 0 ? null : $"{Raw(area, path)}?format={FileLinks.ZipFormat}",
            Flash = flash,
            CanPrune = path.Length == 0 && area is "jobs" or "batches" && entries.Any(entry => JobStates.HasEnded(entry.State)),
        };
    }

    public static BrowseView Unavailable(string area, string path, BridgeError error, Flash? flash) => new()
    {
        Area = area,
        Title = TitleOf(area),
        Path = path,
        Crumbs = Crumbs(area, path),
        Flash = flash,
        Problem = error.Message,
    };

    public static FileView File(string area, string path, long bytes, string? text) => new()
    {
        Area = area,
        Title = TitleOf(area),
        Name = path.Split('/')[^1],
        Folder = path.Contains('/') ? path[..path.LastIndexOf('/')] : string.Empty,
        Crumbs = Crumbs(area, path),
        Bytes = bytes,
        RawHref = Raw(area, path),
        DownloadHref = $"{Raw(area, path)}?download=true",
        IsImage = IsImage(path),
        Text = text,
    };

    public static Flash? FlashOf(int? deleted, long? freed, string? cancelled, string? problem) =>
        problem is not null ? new Flash(problem, IsError: true)
        : deleted is { } count ? new Flash(Messages.DeletedFromThePage(count, Readable.Size(freed ?? 0)), IsError: false)
        : cancelled is not null ? new Flash(Messages.CancelledFromThePage(cancelled), IsError: false)
        : null;

    public static IReadOnlyList<StatusGroup> Status(JsonObject report, JsonObject? storage, JsonObject? jobs, ServerConfig config, TimeSpan running)
    {
        var blender = report["blender"] as JsonObject ?? [];
        var addOn = report["addOn"] as JsonObject ?? [];
        var traffic = report["traffic"] as JsonObject ?? [];
        var isReachable = blender["reachable"]?.GetValue<bool>() == true;

        return
        [
            new StatusGroup("Blender",
            [
                isReachable
                    ? new StatusRow("Blender", $"answers · {blender["blender"]}")
                    : new StatusRow("Blender", $"does not answer · {blender["error"]}", IsProblem: true),
                AddOnRow(addOn),
                new StatusRow("Busy with", blender["executing"]?.GetValue<string>() ?? "nothing"),
                new StatusRow("Machine", $"{blender["machine"]?["platform"]} · {(blender["machine"]?["background"]?.GetValue<bool>() == true ? "no interface" : "with interface")}"),
            ]),
            new StatusGroup("Scene",
            [
                new StatusRow("Open file", OpenFile(storage)),
                new StatusRow("Changes", storage?["modified"]?.GetValue<bool>() == true ? "not saved" : "saved"),
                GpuRow(storage?["compute"] as JsonObject),
                new StatusRow("Render jobs", JobCounts(jobs)),
            ]),
            new StatusGroup("Volume", [.. VolumeRows(storage)]),
            new StatusGroup("Server",
            [
                new StatusRow("Version", report["server"]?["version"]?.GetValue<string>() ?? string.Empty),
                new StatusRow("Address", config.Http.PublicUrl ?? config.Http.Url),
                new StatusRow("Running for", Readable.Duration(running)),
                new StatusRow("Requests to Blender", $"{traffic["requests"]} · {traffic["failures"]} failed", IsProblem: traffic["failures"]?.GetValue<int>() > 0),
                new StatusRow("File links", $"valid for {config.Http.LinkMinutes} min"),
            ]),
        ];
    }

    private static EntryView Entry(string area, string folder, string directory, string? open, JsonObject entry)
    {
        var name = entry["name"]?.GetValue<string>() ?? string.Empty;
        var kind = entry["kind"]?.GetValue<string>();
        var path = Join(folder, name);
        var job = entry["job"] as JsonObject;
        var batch = entry["batch"] as JsonObject;
        var snapshot = entry["snapshot"] as JsonObject;
        var state = job?["state"]?.GetValue<string>() ?? batch?["state"]?.GetValue<string>();
        var isOpen = open is not null && Contains($"{directory}/{name}", open);

        return new EntryView
        {
            Name = name,
            Href = kind == "snapshot" ? null : Page(area, path),
            DownloadHref = kind switch
            {
                "snapshot" => $"{Raw(area, $"{name}.blend")}?download=true",
                "directory" => $"{Raw(area, path)}?format={FileLinks.ZipFormat}",
                _ => $"{Raw(area, path)}?download=true",
            },
            IsFolder = kind == "directory",
            Bytes = entry["bytes"]?.GetValue<long>() ?? 0,
            Files = entry["files"]?.GetValue<int>() ?? 0,
            Modified = entry["modified"]?.GetValue<long>() is { } seconds ? DateTimeOffset.FromUnixTimeSeconds(seconds) : null,
            State = state,
            Detail = job is not null ? $"{job["frames_done"]} of {job["frames_total"]} frames"
                : batch is not null ? $"{batch["steps_done"]} of {batch["steps_total"]} steps"
                : snapshot?["note"]?.GetValue<string>() ?? snapshot?["saved_at"]?.GetValue<string>(),
            InUse = isOpen ? "open in Blender" : JobStates.IsActive(state) ? state : null,
        };
    }

    /// <summary>Whether a path on Blender's machine is, or holds, another; either separator, either case of a Windows drive.</summary>
    private static bool Contains(string parent, string child)
    {
        var normalParent = parent.Replace('\\', '/').TrimEnd('/');
        var normalChild = child.Replace('\\', '/');

        return string.Equals(normalParent, normalChild, StringComparison.OrdinalIgnoreCase)
            || normalChild.StartsWith($"{normalParent}/", StringComparison.OrdinalIgnoreCase);
    }

    private static StatusRow AddOnRow(JsonObject addOn) => addOn["status"]?.GetValue<string>() switch
    {
        "current" => new StatusRow("Add-on", $"the build this server ships · {addOn["installed"]?["digest"]}"),
        "differs" => new StatusRow("Add-on", $"differs from this server's · installed {addOn["installed"]?["digest"]}, shipped {addOn["bundled"]?["digest"]}", IsProblem: true),
        "older" => new StatusRow("Add-on", $"older than this server · speaks {addOn["protocol"]?["addOn"]} against {addOn["protocol"]?["server"]}, installed {addOn["installed"]?["digest"]}", IsProblem: true),
        _ => new StatusRow("Add-on", "unknown", IsProblem: true),
    };

    private static string OpenFile(JsonObject? storage)
    {
        var open = storage?["open_file"]?.GetValue<string>();
        var files = (storage?["areas"] as JsonArray)?.OfType<JsonObject>().FirstOrDefault(area => area["area"]?.GetValue<string>() == "files")?["directory"]?.GetValue<string>();

        return open is null ? "none: an unsaved scene"
            : files is not null && Contains(files, open) ? open.Replace('\\', '/')[(files.Replace('\\', '/').TrimEnd('/').Length + 1)..]
            : open;
    }

    private static StatusRow GpuRow(JsonObject? compute)
    {
        if (compute?["backend"]?.GetValue<string>() is not { } backend)
        {
            return new StatusRow("GPU", "not chosen: blender_set_cycles with a backend chooses one");
        }

        var devices = (compute["devices"] as JsonArray ?? []).OfType<JsonObject>().Where(device => device["use"]?.GetValue<bool>() == true).Select(device => device["name"]?.GetValue<string>());

        return new StatusRow("GPU", backend == "NONE" ? "none: Cycles renders on the CPU" : $"{backend} · {string.Join(", ", devices)}", IsProblem: backend == "NONE");
    }

    private static string JobCounts(JsonObject? jobs)
    {
        var states = (jobs?["entries"] as JsonArray ?? [])
            .OfType<JsonObject>()
            .Select(entry => entry["job"]?["state"]?.GetValue<string>())
            .OfType<string>()
            .GroupBy(state => state, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => $"{group.Count()} {group.Key}")
            .ToList();

        return states.Count == 0 ? "none" : string.Join(" · ", states);
    }

    private static IEnumerable<StatusRow> VolumeRows(JsonObject? storage)
    {
        if (storage is null)
        {
            yield return new StatusRow("Volume", "unknown while Blender does not answer", IsProblem: true);
            yield break;
        }

        foreach (var area in (storage["areas"] as JsonArray ?? []).OfType<JsonObject>())
        {
            yield return new StatusRow(area["area"]?.GetValue<string>() ?? string.Empty, $"{Readable.Size(area["bytes"]?.GetValue<long>() ?? 0)} · {area["files"]} files");
        }

        var free = storage["disk"]?["free_bytes"]?.GetValue<long>() ?? 0;
        var total = storage["disk"]?["total_bytes"]?.GetValue<long>() ?? 0;

        yield return new StatusRow("Disk", $"{Readable.Size(free)} free of {Readable.Size(total)}", IsProblem: total > 0 && free < total / 20);
    }
}
