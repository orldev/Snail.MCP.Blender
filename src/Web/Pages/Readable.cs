using System.Globalization;

namespace Snail.MCP.Blender.Web.Pages;

/// <summary>Sizes and durations the way a person reads them on a page.</summary>
public static class Readable
{
    private static readonly string[] Units = ["B", "KB", "MB", "GB", "TB"];

    public static string Size(long bytes)
    {
        var value = (double)bytes;
        var unit = 0;

        while (value >= 1024 && unit < Units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        return unit == 0 ? $"{bytes} B" : $"{value.ToString(value >= 100 ? "0" : "0.#", CultureInfo.InvariantCulture)} {Units[unit]}";
    }

    public static string Duration(TimeSpan span) =>
        span.TotalHours >= 1 ? $"{(int)span.TotalHours} h {span.Minutes} min"
        : span.TotalMinutes >= 1 ? $"{span.Minutes} min"
        : $"{span.Seconds} s";

    /// <summary>A moment in UTC for the page to show; the script puts it into the reader's own zone.</summary>
    public static string Moment(DateTimeOffset? moment) =>
        moment is { } value ? value.UtcDateTime.ToString("yyyy-MM-dd HH:mm 'UTC'", CultureInfo.InvariantCulture) : string.Empty;

    public static string MachineMoment(DateTimeOffset? moment) =>
        moment is { } value ? value.ToString("O", CultureInfo.InvariantCulture) : string.Empty;
}
