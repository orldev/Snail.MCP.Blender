namespace Snail.MCP.Blender.Web.Pages;

/// <summary>A section of the pages, named in the header.</summary>
public sealed record Section(string Label, string Href);

/// <summary>One step of the path above a listing.</summary>
public sealed record Crumb(string Label, string Href);

/// <summary>What the last action did, said once at the top of the page it returned to.</summary>
public sealed record Flash(string Text, bool IsError);

/// <summary>A captioned group of status rows.</summary>
public sealed record StatusGroup(string Caption, IReadOnlyList<StatusRow> Rows);

/// <summary>A label, what it says, and whether what it says is a problem.</summary>
public sealed record StatusRow(string Label, string Value, bool IsProblem = false);

/// <summary>A row of a listing: a file, a folder, a render job, a batch or a snapshot.</summary>
public sealed record EntryView
{
    /// <summary>The name shown in the row, the last segment of its path.</summary>
    public required string Name { get; init; }

    /// <summary>The page the name opens; none for a snapshot, which is only downloaded or deleted.</summary>
    public string? Href { get; init; }

    /// <summary>The link that downloads the entry: the file itself, or a folder as one archive.</summary>
    public required string DownloadHref { get; init; }

    /// <summary>Whether the entry is a folder, which opens as a listing of its own.</summary>
    public bool IsFolder { get; init; }

    /// <summary>How many bytes the entry holds, every file inside counted for a folder.</summary>
    public long Bytes { get; init; }

    /// <summary>How many files a folder holds; zero for a file.</summary>
    public int Files { get; init; }

    /// <summary>When the entry last changed, or null when the add-on did not say.</summary>
    public DateTimeOffset? Modified { get; init; }

    /// <summary>A job's or a batch's state.</summary>
    public string? State { get; init; }

    /// <summary>Frames or steps done, or a snapshot's note.</summary>
    public string? Detail { get; init; }

    /// <summary>Why it cannot be deleted now: open in Blender, or still running.</summary>
    public string? InUse { get; init; }

    /// <summary>Whether the job or batch is still queued or running, which keeps it from being deleted.</summary>
    public bool IsRunning => JobStates.IsActive(State);
}

/// <summary>One folder of an area.</summary>
public sealed record BrowseView
{
    /// <summary>The area of the data directory the folder belongs to: files, jobs, batches or snapshots.</summary>
    public required string Area { get; init; }

    /// <summary>The heading of the page, the area's name as a person reads it.</summary>
    public required string Title { get; init; }

    /// <summary>The folder's path inside its area, empty for the area's root.</summary>
    public required string Path { get; init; }

    /// <summary>The steps from the area's root down to this folder.</summary>
    public required IReadOnlyList<Crumb> Crumbs { get; init; }

    /// <summary>The rows of the listing, folders and files alike.</summary>
    public IReadOnlyList<EntryView> Entries { get; init; } = [];

    /// <summary>Whether the add-on cut the listing short, so the page shows only part of the folder.</summary>
    public bool IsTruncated { get; init; }

    /// <summary>The link that downloads the whole folder as one zip, or null where there is nothing to pack.</summary>
    public string? ZipHref { get; init; }

    /// <summary>What the last action on this page did, shown once above the listing.</summary>
    public Flash? Flash { get; init; }

    /// <summary>Why the folder could not be listed.</summary>
    public string? Problem { get; init; }

    /// <summary>Jobs and batches can clear everything that has ended in one go.</summary>
    public bool CanPrune { get; init; }

    /// <summary>Whether the rows are render jobs or batches rather than files.</summary>
    public bool HasJobs => Area is "jobs" or "batches";

    /// <summary>How many bytes the listed rows hold together.</summary>
    public long Bytes => Entries.Sum(entry => entry.Bytes);
}

/// <summary>One file of an area, with what can be shown of it.</summary>
public sealed record FileView
{
    /// <summary>The area of the data directory the file belongs to.</summary>
    public required string Area { get; init; }

    /// <summary>The heading of the page, the area's name as a person reads it.</summary>
    public required string Title { get; init; }

    /// <summary>The file's name, the last segment of its path.</summary>
    public required string Name { get; init; }

    /// <summary>The folder the file lies in, inside its area; empty at the area's root.</summary>
    public required string Folder { get; init; }

    /// <summary>The steps from the area's root down to the file.</summary>
    public required IReadOnlyList<Crumb> Crumbs { get; init; }

    /// <summary>How many bytes the file holds.</summary>
    public long Bytes { get; init; }

    /// <summary>The link that serves the file as it is, which an image or a video plays inline from.</summary>
    public required string RawHref { get; init; }

    /// <summary>The link that serves the file as a download.</summary>
    public required string DownloadHref { get; init; }

    /// <summary>Whether the file is a picture the page shows inline.</summary>
    public bool IsImage { get; init; }

    /// <summary>The file's text when it is small text; null otherwise.</summary>
    public string? Text { get; init; }
}
