using Microsoft.Extensions.Time.Testing;
using Snail.MCP.Blender.Application.Access;
using Snail.MCP.Blender.Configuration;

namespace Snail.MCP.Blender.Tests.Application;

public class FileLinksTests
{
    private static readonly FakeTimeProvider Time = new(DateTimeOffset.Parse("2026-09-16T12:00:00Z"));

    [Fact]
    public void For_BuildsOnThePublicUrl_AndEscapesEverySegment()
    {
        var links = Links(new ServerConfig { Http = { Token = "t", Url = "http://0.0.0.0:8080", PublicUrl = "https://blender.example.com/" } });

        var link = links.For("GET", "files", "мой проект/renders/f 1.png");

        Assert.StartsWith("https://blender.example.com/raw/files/%D0%BC%D0%BE%D0%B9%20%D0%BF%D1%80%D0%BE%D0%B5%D0%BA%D1%82/renders/f%201.png?expires=", link.Url, StringComparison.Ordinal);
        Assert.Equal(Time.GetUtcNow().AddMinutes(15), link.Expires);
    }

    /// <summary>curl -I checks a link with HEAD; the format is signed, so a link to a folder as a tar does not also hand it out as a zip.</summary>
    [Fact]
    public void Verifies_HeadForAGetLink_ButNotAnotherFormatOrToken()
    {
        var config = new ServerConfig { Http = { Token = "t" } };
        var link = new Uri(Links(config).For("GET", "files", "robot/renders", "tar").Url);
        var query = System.Web.HttpUtility.ParseQueryString(link.Query);
        var expires = long.Parse(query["expires"]!);
        var signature = query["signature"]!;

        Assert.True(Links(config).Verifies("HEAD", "/raw/files/robot/renders", "tar", overwrite: false, expires, signature));
        Assert.False(Links(config).Verifies("GET", "/raw/files/robot/renders", "zip", overwrite: false, expires, signature));
        Assert.False(Links(new ServerConfig { Http = { Token = "rotated" } }).Verifies("GET", "/raw/files/robot/renders", "tar", overwrite: false, expires, signature));
    }

    /// <summary>A link to write a file that is not there yet cannot be turned into one that replaces the file that is: the word is signed, not
    /// merely read, so adding it to the query invalidates the link rather than widening it.</summary>
    [Fact]
    public void Verifies_ALinkSignedToKeepWhatIsThere_RefusesTheSameRequestAskingToReplaceIt()
    {
        var config = new ServerConfig { Http = { Token = "t" } };
        var query = System.Web.HttpUtility.ParseQueryString(new Uri(Links(config).For("PUT", "files", "robot/shot.png").Url).Query);
        var expires = long.Parse(query["expires"]!);
        var signature = query["signature"]!;

        Assert.True(Links(config).Verifies("PUT", "/raw/files/robot/shot.png", null, overwrite: false, expires, signature));
        Assert.False(Links(config).Verifies("PUT", "/raw/files/robot/shot.png", null, overwrite: true, expires, signature));
    }

    /// <summary>The link the upload command carries says so in its own query, so curl sends what was signed.</summary>
    [Fact]
    public void For_WithOverwrite_CarriesItInTheQueryItSigned()
    {
        var config = new ServerConfig { Http = { Token = "t" } };
        var link = new Uri(Links(config).For("PUT", "files", "robot/shot.png", overwrite: true).Url);
        var query = System.Web.HttpUtility.ParseQueryString(link.Query);

        Assert.Equal("true", query["overwrite"]);
        Assert.True(Links(config).Verifies("PUT", "/raw/files/robot/shot.png", null, overwrite: true, long.Parse(query["expires"]!), query["signature"]!));
    }

    private static FileLinks Links(ServerConfig config) => new(new ClientToken(config), config, Time);
}
