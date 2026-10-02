using Microsoft.Extensions.Time.Testing;
using Snail.MCP.Blender.Application.Access;
using Snail.MCP.Blender.Configuration;

namespace Snail.MCP.Blender.Tests.Application;

/// <summary>The keys clients are admitted by: minted once, kept as digests, revoked without touching anyone else.</summary>
public class ClientKeysTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"snail-keys-{Guid.NewGuid().ToString("N")[..8]}");

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }

        GC.SuppressFinalize(this);
    }

    [Fact]
    public void Key_JustMinted_AdmitsItsHolder_AndNobodyElse()
    {
        var keys = Build();
        var (key, secret) = keys.Mint("lighting", [Scopes.Read, Scopes.Write]);

        Assert.Equal("lighting", keys.Holder(secret)?.Name);
        Assert.Null(keys.Holder($"l{secret[1..]}"));
        Assert.Null(keys.Holder(""));
        Assert.True(key.May(Scopes.Write));
        Assert.False(key.May(Scopes.Python));
    }

    /// <summary>The file is of no use to whoever reads it: it holds the digest of a key, never the key.</summary>
    [Fact]
    public void Secret_OfAMintedKey_IsNowhereInTheFile()
    {
        var (_, secret) = Build().Mint("lighting");

        Assert.DoesNotContain(secret, File.ReadAllText(Path.Combine(_directory, "state", "clients.json")), StringComparison.Ordinal);
    }

    [Fact]
    public void Key_ThatWasRevoked_AdmitsNobody_AndLeavesTheOthersAdmitted()
    {
        var keys = Build();
        var (_, revoked) = keys.Mint("lighting");
        var (_, kept) = keys.Mint("modeling");

        Assert.True(keys.Revoke("lighting"));
        Assert.Null(keys.Holder(revoked));
        Assert.Equal("modeling", keys.Holder(kept)?.Name);
        Assert.False(keys.Revoke("lighting"));
    }

    /// <summary>A server set up with one token keeps working, and that token opens everything, as it always did.</summary>
    [Fact]
    public void Token_TheServerWasConfiguredWith_IsAKeyOfItsOwn_WithEveryScope()
    {
        var keys = Build(token: "http-token", agent: "the-studio");

        Assert.Equal("the-studio", keys.Holder("http-token")?.Name);
        Assert.Equal(Scopes.All, keys.Holder("http-token")!.Scopes);
        Assert.Null(keys.Holder("another"));
    }

    [Fact]
    public void Keys_MintedBefore_AreReadBackByTheNextServer()
    {
        var (_, secret) = Build().Mint("lighting", [Scopes.Read]);

        Assert.Equal([Scopes.Read], Build().Holder(secret)!.Scopes);
    }

    [Fact]
    public void Mint_WithAScopeThereIsNone_SaysSoRatherThanMintingIt()
    {
        var keys = Build();

        var refused = Assert.Throws<ArgumentException>(() => keys.Mint("lighting", ["everything"]));

        Assert.Contains("no such scope", refused.Message, StringComparison.Ordinal);
        Assert.Empty(keys.All);
    }

    private ClientKeys Build(string? token = null, string? agent = null) =>
        new(
            new ServerConfig { DataDirectory = _directory, Http = new HttpOptions { Token = token }, Bridge = new BlenderLinkOptions { Agent = agent } },
            new FakeTimeProvider());
}
