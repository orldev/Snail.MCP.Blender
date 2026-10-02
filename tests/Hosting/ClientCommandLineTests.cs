using Snail.MCP.Blender.Application.Access;
using Snail.MCP.Blender.Configuration;
using Snail.MCP.Blender.Hosting;

namespace Snail.MCP.Blender.Tests.Hosting;

/// <summary>The operator's whole interface to the keys: what it prints, what it writes and what it refuses.</summary>
public class ClientCommandLineTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"snail-cli-{Guid.NewGuid().ToString("N")[..8]}");

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }

        GC.SuppressFinalize(this);
    }

    [Fact]
    public void Add_PrintsTheSecretOnce_AndTheKeyAdmitsItsHolder()
    {
        var printed = Run("clients", "add", "lighting", "--scope", "read,write");
        var secret = printed.Split('\n')[1].Trim();

        Assert.Contains("lighting — read, write", printed, StringComparison.Ordinal);
        Assert.Contains("only time the key is shown", printed, StringComparison.Ordinal);
        Assert.Equal("lighting", Keys().Holder(secret)?.Name);
        Assert.DoesNotContain(secret, File.ReadAllText(Path.Combine(_directory, "state", "clients.json")), StringComparison.Ordinal);
    }

    [Fact]
    public void List_NamesEveryClient_AndWhatItMayDo()
    {
        Run("clients", "add", "lighting", "--scope", "read");
        Run("clients", "add", "modeling");

        var listed = Run("clients", "list");

        Assert.Contains("lighting — read,", listed, StringComparison.Ordinal);
        Assert.Contains("modeling — read, write, delete, farm,", listed, StringComparison.Ordinal);
    }

    [Fact]
    public void Revoke_StopsAdmittingThatClient_AndSaysSoWhenThereIsNone()
    {
        var secret = Run("clients", "add", "lighting").Split('\n')[1].Trim();

        var revoked = Run("clients", "revoke", "lighting");
        var again = Run("clients", "revoke", "lighting", expecting: 1);

        Assert.Contains("no longer admitted", revoked, StringComparison.Ordinal);
        Assert.Contains("no client named", again, StringComparison.Ordinal);
        Assert.Null(Keys().Holder(secret));
    }

    [Fact]
    public void Add_WithAScopeThereIsNone_SaysWhichScopesThereAre_AndMintsNothing()
    {
        var refused = Run("clients", "add", "lighting", "--scope", "everything", expecting: 1);

        Assert.Contains("no such scope: everything", refused, StringComparison.Ordinal);
        Assert.Empty(Keys().All);
    }

    [Fact]
    public void Verb_ThatIsNotOne_PrintsTheUsage()
    {
        var usage = Run("clients", "frobnicate", expecting: 1);

        Assert.Contains("usage: clients list", usage, StringComparison.Ordinal);
    }

    [Fact]
    public void Asked_IsOnlyTheClientsVerb()
    {
        Assert.True(ClientCommandLine.Asked(["clients", "list"]));
        Assert.True(ClientCommandLine.Asked(["CLIENTS"]));
        Assert.False(ClientCommandLine.Asked([]));
        Assert.False(ClientCommandLine.Asked(["--transport", "http"]));
    }

    private string Run(params string[] args) => Run(args, expecting: 0);

    private string Run(string[] args, int expecting)
    {
        var printed = new StringWriter();

        Assert.Equal(expecting, ClientCommandLine.Run(args, printed, new ServerConfig { DataDirectory = _directory }));

        return printed.ToString();
    }

    private string Run(string first, string second, int expecting) => Run([first, second], expecting);

    private string Run(string first, string second, string third, int expecting) => Run([first, second, third], expecting);

    private string Run(string first, string second, string third, string fourth, string fifth, int expecting) =>
        Run([first, second, third, fourth, fifth], expecting);

    private ClientKeys Keys() => new(new ServerConfig { DataDirectory = _directory }, TimeProvider.System);
}
