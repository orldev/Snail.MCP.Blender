using Snail.MCP.Blender.Application.Access;
using Snail.MCP.Blender.Configuration;

namespace Snail.MCP.Blender.Tests.Application;

/// <summary>The secret an HTTP client proves itself with: where it is read from, who it lets in, and the key links are signed with.</summary>
public class ClientTokenTests : IDisposable
{
    private readonly string _file = Path.Combine(TestFolder.Create(), "token");

    public void Dispose()
    {
        File.Delete(_file);
        GC.SuppressFinalize(this);
    }

    /// <summary>A container receives its secret as a file, and the file ends with the newline whatever wrote it left behind.</summary>
    [Fact]
    public void ATokenFile_IsReadWithoutItsSurroundingWhitespace()
    {
        File.WriteAllText(_file, "  a-secret\n");

        var token = new ClientToken(new ServerConfig { Http = { TokenFile = _file } });

        Assert.True(token.Accepts("a-secret"));
        Assert.False(token.Accepts("a-secret\n"));
    }

    [Fact]
    public void AConfiguredToken_IsPreferredOverTheFile()
    {
        File.WriteAllText(_file, "from-the-file");

        var token = new ClientToken(new ServerConfig { Http = { Token = "from-the-settings", TokenFile = _file } });

        Assert.True(token.Accepts("from-the-settings"));
        Assert.False(token.Accepts("from-the-file"));
    }

    /// <summary>An empty file is a secret that was never written, not a secret that is empty: a server that read it as one would let in a client presenting nothing.</summary>
    [Fact]
    public void AnEmptyTokenFile_LeavesTheServerWithNoToken()
    {
        File.WriteAllText(_file, "   \n");

        var token = new ClientToken(new ServerConfig { Http = { TokenFile = _file } });

        Assert.False(token.Accepts(string.Empty));
        Assert.False(token.Accepts("anything"));
    }

    [Fact]
    public void ATokenFileThatIsNotThere_LeavesTheServerWithNoToken()
    {
        var token = new ClientToken(new ServerConfig { Http = { TokenFile = Path.Combine(TestFolder.Create(), "missing-token") } });

        Assert.False(token.Accepts("anything"));
        Assert.Equal(32, token.LinkKey.Length);
    }

    /// <summary>A link survives a restart of the container because the key is derived from the token rather than drawn at random; a new token ends every link signed with the old one.</summary>
    [Fact]
    public void TheLinkKey_FollowsTheToken_SoLinksOutliveARestartAndDieWithTheSecret()
    {
        var first = new ClientToken(new ServerConfig { Http = { Token = "a-secret" } });
        var afterARestart = new ClientToken(new ServerConfig { Http = { Token = "a-secret" } });
        var afterANewSecret = new ClientToken(new ServerConfig { Http = { Token = "another-secret" } });

        Assert.Equal(first.LinkKey, afterARestart.LinkKey);
        Assert.NotEqual(first.LinkKey, afterANewSecret.LinkKey);
    }
}
