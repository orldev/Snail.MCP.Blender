using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using Snail.MCP.Blender.Configuration;

namespace Snail.MCP.Blender.Application.Access;

/// <summary>A link that opens one path of the volume for one method until it expires.</summary>
public sealed record FileLink(string Url, DateTimeOffset Expires);

/// <summary>Signs and checks links to what the HTTP server serves under <c>/raw</c>, so a command the agent runs on its own machine needs no token.</summary>
/// <remarks>The signature covers the method, the path, the format, whether it may replace what is there, and the expiry: a link to read a
/// render cannot overwrite it, open the folder around it or outlive its minutes, and one issued to write a new file cannot be turned into one
/// that replaces an existing file by adding a word to the query. It ends up in the agent's transcript, which is why it carries no token and
/// dies on its own.</remarks>
public sealed class FileLinks(ClientToken token, ServerConfig config, TimeProvider time)
{
    public const string RawPrefix = "/raw";

    /// <summary>A folder read as, or a body written as, one tar.</summary>
    public const string TarFormat = "tar";

    /// <summary>A folder read as one zip, for a browser.</summary>
    public const string ZipFormat = "zip";

    private const long LatestUnixSeconds = 253402300799;

    /// <summary>A link to a path of an area; the format names the archive a folder comes as, or that a body is a tar to unpack, and
    /// <c>overwrite</c> says whether it may replace what is already there — signed like the rest, so it cannot be added afterwards.</summary>
    public FileLink For(string method, string area, string path, string? format = null, bool overwrite = false)
    {
        var expires = time.GetUtcNow().AddMinutes(config.Http.LinkMinutes);
        var seconds = expires.ToUnixTimeSeconds();
        var route = Route(area, path);
        var escaped = string.Join('/', route.Split('/').Select(Uri.EscapeDataString));
        var formatQuery = format is null ? string.Empty : $"&format={Uri.EscapeDataString(format)}";
        var replacing = overwrite ? "&overwrite=true" : string.Empty;

        return new FileLink(
            $"{BaseUrl()}{escaped}?expires={seconds}&signature={Sign(method, route, format, overwrite, seconds)}{formatQuery}{replacing}",
            expires);
    }

    /// <summary>Whether a request carries a link signed for exactly its method, path, format and replacing, that has not expired.</summary>
    public bool Verifies(string method, string path, string? format, bool overwrite, long expires, string signature)
    {
        if (expires is < 0 or > LatestUnixSeconds || DateTimeOffset.FromUnixTimeSeconds(expires) < time.GetUtcNow())
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(Sign(method, path, format, overwrite, expires)), Encoding.ASCII.GetBytes(signature));
    }

    /// <summary>The path a request for an area's file arrives at, before escaping.</summary>
    public static string Route(string area, string path) => $"{RawPrefix}/{area}/{path.Trim('/')}".TrimEnd('/');

    private string Sign(string method, string path, string? format, bool overwrite, long expires) =>
        Base64Url.EncodeToString(HMACSHA256.HashData(token.LinkKey, Encoding.UTF8.GetBytes($"{Canonical(method)}\n{path}\n{format}\n{(overwrite ? "replace" : "keep")}\n{expires}")));

    /// <summary>curl -I asks with HEAD what a link opens with GET.</summary>
    private static string Canonical(string method) =>
        string.Equals(method, "HEAD", StringComparison.OrdinalIgnoreCase) ? "GET" : method.ToUpperInvariant();

    private string BaseUrl() => (string.IsNullOrWhiteSpace(config.Http.PublicUrl) ? config.Http.Url : config.Http.PublicUrl).TrimEnd('/');
}
