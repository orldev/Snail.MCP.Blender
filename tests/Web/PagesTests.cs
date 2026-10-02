using Snail.MCP.Blender.Application.Access;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Builder;

namespace Snail.MCP.Blender.Tests.Web;

/// <summary>The pages of the server beside Blender, against an add-on that keeps the volume in memory: signing in, looking through the areas, and cleaning them.</summary>
public sealed partial class PagesTests : IAsyncLifetime
{
    private readonly InMemoryVolume _volume = new()
    {
        Files =
        {
            ["files/robot/robot.blend"] = "blend"u8.ToArray(),
            ["files/robot/notes.txt"] = "keep the key light warm"u8.ToArray(),
            ["files/robot/renders/f_0001.png"] = new byte[2048],
            ["jobs/job-1/log.txt"] = "frame 3"u8.ToArray(),
            ["jobs/job-2/log.txt"] = "done"u8.ToArray(),
        },
        Jobs = { ["job-1"] = "running", ["job-2"] = "finished" },
        OpenFile = "files/robot/robot.blend",
    };

    private readonly CookieContainer _cookies = new();
    private WebApplication _app = null!;
    private HttpClient _browser = null!;

    public async Task InitializeAsync()
    {
        _app = await InProcessHttpServer.StartAsync(_volume.AddOn.Port);
        _browser = new HttpClient(new HttpClientHandler { CookieContainer = _cookies, AllowAutoRedirect = false }) { BaseAddress = new Uri(_app.Urls.First()) };
    }

    public async Task DisposeAsync()
    {
        _browser.Dispose();
        await _app.DisposeAsync();
        await _volume.DisposeAsync();
    }

    [Fact]
    public async Task Pages_WithoutASession_SendToSignIn_AndOnlyTheClientTokenOpensThem()
    {
        var anonymous = await _browser.GetAsync("/");
        var refused = await PostAsync("/login", "/login", new() { ["token"] = "guess", ["returnUrl"] = "/" });
        var signedIn = await PostAsync("/login", "/login", new() { ["token"] = InProcessHttpServer.Token, ["returnUrl"] = "/" });
        var status = await _browser.GetStringAsync("/");

        Assert.Equal(HttpStatusCode.Redirect, anonymous.StatusCode);
        Assert.Equal("/login?returnUrl=%2F", anonymous.Headers.Location!.OriginalString);
        Assert.Equal(HttpStatusCode.Unauthorized, refused.StatusCode);
        Assert.Contains("not this server", await refused.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        Assert.Equal("/", signedIn.Headers.Location!.OriginalString);
        Assert.Contains("OPTIX · NVIDIA RTX A2000 12GB", status, StringComparison.Ordinal);
        Assert.Contains("robot/robot.blend", status, StringComparison.Ordinal);
    }

    /// <summary>Revoking a key ends the browser session it opened, at the next request rather than at the end of the working day.</summary>
    [Fact]
    public async Task Session_OfAKeyThatWasRevoked_EndsAtTheNextRequest()
    {
        var keys = _app.Services.GetRequiredService<ClientKeys>();
        var (_, secret) = keys.Mint("lighting");

        var signedIn = await PostAsync("/login", "/login", new() { ["token"] = secret, ["returnUrl"] = "/" });
        var admitted = await _browser.GetAsync("/");

        Assert.True(keys.Revoke("lighting"));

        var afterwards = await _browser.GetAsync("/");

        Assert.Equal("/", signedIn.Headers.Location!.OriginalString);
        Assert.Equal(HttpStatusCode.OK, admitted.StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, afterwards.StatusCode);
        Assert.StartsWith("/login", afterwards.Headers.Location!.OriginalString, StringComparison.Ordinal);
    }

    /// <summary>A return address from the query is followed only while it stays on this site.</summary>
    [Fact]
    public async Task SignIn_WithAReturnAddressOnAnotherSite_ReturnsHome()
    {
        var signedIn = await PostAsync("/login", "/login", new() { ["token"] = InProcessHttpServer.Token, ["returnUrl"] = "//evil.example/steal" });

        Assert.Equal("/", signedIn.Headers.Location!.OriginalString);
    }

    [Fact]
    public async Task Files_ListAFolder_MarkTheOpenFile_AndShowASmallTextFile()
    {
        await SignInAsync();

        var folder = await _browser.GetStringAsync("/files/robot");
        var file = await _browser.GetStringAsync("/files/robot/notes.txt");

        Assert.Contains("href=\"/files/robot/renders\"", folder, StringComparison.Ordinal);
        Assert.Contains("open in Blender", folder, StringComparison.Ordinal);
        Assert.Contains("keep the key light warm", file, StringComparison.Ordinal);
    }

    /// <summary>A deletion is a form: without the token the page put in it, a signed-in browser cannot be made to delete from another site.</summary>
    [Fact]
    public async Task Delete_WithoutTheFormsToken_IsRefused_AndWithIt_RemovesWhatWasTicked_UntilBlenderRefuses()
    {
        await SignInAsync();

        var forged = await _browser.PostAsync("/delete", new FormUrlEncodedContent(new Dictionary<string, string> { ["area"] = "files", ["path"] = "robot", ["only"] = "notes.txt" }));
        var deleted = await PostAsync("/delete", "/files/robot", new() { ["area"] = "files", ["path"] = "robot" }, [("names", "renders"), ("names", "robot.blend")]);
        var page = await _browser.GetStringAsync(deleted.Headers.Location!.OriginalString);

        Assert.Equal(HttpStatusCode.BadRequest, forged.StatusCode);
        Assert.Contains("files/robot/notes.txt", _volume.Files.Keys);
        Assert.DoesNotContain("files/robot/renders/f_0001.png", _volume.Files.Keys);
        Assert.Contains("files/robot/robot.blend", _volume.Files.Keys);
        Assert.StartsWith("/files/robot?deleted=1&freed=2048&problem=", deleted.Headers.Location!.OriginalString, StringComparison.Ordinal);
        Assert.Contains("open in Blender", page, StringComparison.Ordinal);
    }

    /// <summary>The folder of the delete form was never checked, and the names only against exactly "." and "..": "x/.." with a job's name, or a
    /// folder with ".. ", reached a running job or the area root once the add-on had trimmed and resolved it.</summary>
    [Fact]
    public async Task Delete_FromAFolderThatClimbsOrOfANamePaddedWithSpaces_IsRefusedBeforeBlender()
    {
        await SignInAsync();

        var climbing = await PostAsync("/delete", "/jobs", new() { ["area"] = "jobs", ["path"] = "x/.." }, [("names", "job-1")]);
        var padded = await PostAsync("/delete", "/files/robot", new() { ["area"] = "files", ["path"] = "robot", ["only"] = ".. " });

        Assert.Contains("problem=", climbing.Headers.Location!.OriginalString, StringComparison.Ordinal);
        Assert.Contains("problem=", padded.Headers.Location!.OriginalString, StringComparison.Ordinal);
        Assert.DoesNotContain(_volume.AddOn.Received, request => request["command"]!.ToString() == "storage_delete");
    }

    [Fact]
    public async Task Jobs_OnlyARunningJobCanBeCancelled_AndEverythingThatEndedGoesInOneAction()
    {
        await SignInAsync();

        var jobs = await _browser.GetStringAsync("/jobs");
        var pruned = await PostAsync("/prune", "/jobs", new() { ["area"] = "jobs" });

        Assert.Single(CancelButtons().Matches(jobs));
        Assert.Contains("value=\"job-1\"", CancelButtons().Match(jobs).Value, StringComparison.Ordinal);
        Assert.Equal("/jobs?deleted=1&freed=4", pruned.Headers.Location!.OriginalString);
        Assert.Contains("jobs/job-1/log.txt", _volume.Files.Keys);
        Assert.DoesNotContain("jobs/job-2/log.txt", _volume.Files.Keys);
    }

    [Fact]
    public async Task Raw_WithASession_ReadsFiles_ButCannotDeleteThem()
    {
        await SignInAsync();

        var read = await _browser.GetAsync("/raw/files/robot/notes.txt");
        var deleted = await _browser.DeleteAsync("/raw/files/robot/notes.txt");

        Assert.Equal(HttpStatusCode.OK, read.StatusCode);
        Assert.Equal("sandbox", read.Headers.GetValues("Content-Security-Policy").Single());
        Assert.Equal(HttpStatusCode.Unauthorized, deleted.StatusCode);
        Assert.Contains("files/robot/notes.txt", _volume.Files.Keys);
    }

    /// <summary>The pages wear the documentation's own stylesheet, embedded at build time, so the two cannot drift apart.</summary>
    [Fact]
    public async Task Assets_ServeTheDocumentationsStylesheet_WithoutASession()
    {
        var stylesheet = await _browser.GetStringAsync("/assets/site.css");
        var documentation = await File.ReadAllTextAsync(Path.Combine(Repository.Root, "docs", "theme", "css", "site.css"));

        Assert.Equal(documentation, stylesheet);
    }

    private async Task SignInAsync()
    {
        var signedIn = await PostAsync("/login", "/login", new() { ["token"] = InProcessHttpServer.Token, ["returnUrl"] = "/" });

        Assert.Equal(HttpStatusCode.Redirect, signedIn.StatusCode);
    }

    /// <summary>Posts a form the way a browser does: with the antiforgery token the page it came from carries.</summary>
    private async Task<HttpResponseMessage> PostAsync(string action, string page, Dictionary<string, string> fields, IReadOnlyList<(string Name, string Value)>? repeated = null)
    {
        var html = await _browser.GetStringAsync(page);
        var token = FormToken().Match(html).Groups["value"].Value;
        var form = fields.Select(field => new KeyValuePair<string, string>(field.Key, field.Value))
            .Concat((repeated ?? []).Select(field => new KeyValuePair<string, string>(field.Name, field.Value)))
            .Append(new KeyValuePair<string, string>("__RequestVerificationToken", WebUtility.HtmlDecode(token)));

        return await _browser.PostAsync(action, new FormUrlEncodedContent(form));
    }


    [GeneratedRegex("name=\"__RequestVerificationToken\" value=\"(?<value>[^\"]+)\"")]
    private static partial Regex FormToken();

    [GeneratedRegex("<button[^>]*formaction=\"/cancel\"[^>]*>")]
    private static partial Regex CancelButtons();
}
