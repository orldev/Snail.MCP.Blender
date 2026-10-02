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
    public required string Name { get; init; }

    /// <summary>The page the name opens; none for a snapshot, which is only downloaded or deleted.</summary>
    public string? Href { get; init; }

    public required string DownloadHref { get; init; }

    public bool IsFolder { get; init; }

    public long Bytes { get; init; }

    public int Files { get; init; }

    public DateTimeOffset? Modified { get; init; }

    /// <summary>A job's or a batch's state.</summary>
    public string? State { get; init; }

    /// <summary>Frames or steps done, or a snapshot's note.</summary>
    public string? Detail { get; init; }

    /// <summary>Why it cannot be deleted now: open in Blender, or still running.</summary>
    public string? InUse { get; init; }

    public bool IsRunning => JobStates.IsActive(State);
}

/// <summary>One folder of an area.</summary>
public sealed record BrowseView
{
    public required string Area { get; init; }

    public required string Title { get; init; }

    public required string Path { get; init; }

    public required IReadOnlyList<Crumb> Crumbs { get; init; }

    public IReadOnlyList<EntryView> Entries { get; init; } = [];

    public bool Truncated { get; init; }

    public string? ZipHref { get; init; }

    public Flash? Flash { get; init; }

    /// <summary>Why the folder could not be listed.</summary>
    public string? Problem { get; init; }

    /// <summary>Jobs and batches can clear everything that has ended in one go.</summary>
    public bool CanPrune { get; init; }

    public bool HasJobs => Area is "jobs" or "batches";

    public long Bytes => Entries.Sum(entry => entry.Bytes);
}

/// <summary>One file of an area, with what can be shown of it.</summary>
public sealed record FileView
{
    public required string Area { get; init; }

    public required string Title { get; init; }

    public required string Name { get; init; }

    public required string Folder { get; init; }

    public required IReadOnlyList<Crumb> Crumbs { get; init; }

    public long Bytes { get; init; }

    public required string RawHref { get; init; }

    public required string DownloadHref { get; init; }

    public bool IsImage { get; init; }

    /// <summary>The file's text when it is small text; null otherwise.</summary>
    public string? Text { get; init; }
}
