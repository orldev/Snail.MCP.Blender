using Snail.MCP.Blender.Application.Sessions;

namespace Snail.MCP.Blender.Tests.Application;

/// <summary>One Blender, several clients: who the request in hand comes from, what its key opens, and what its commands are signed as.</summary>
public class ClientSessionsTests
{
    /// <summary>The scopes in force are the ones of the key the request presented: a key re-minted with narrower scopes is narrower from the next
    /// request on, rather than what the first key of that name opened.</summary>
    [Fact]
    public void Client_PresentingANarrowerKey_LosesWhatTheOldOneOpened()
    {
        var sessions = new ClientSessions(agent: null);

        using (sessions.Serve("claude-code", "lighting", [Scopes.Read, Scopes.Write, Scopes.Python]))
        {
            Assert.True(sessions.Current.May(Scopes.Python));
        }

        using (sessions.Serve("claude-code", "lighting", [Scopes.Read]))
        {
            Assert.Equal([Scopes.Read], sessions.Current.Scopes);
            Assert.False(sessions.Current.May(Scopes.Python));
        }
    }

    /// <summary>The agent the add-on records is the client being served, not the one the server was started with.</summary>
    [Fact]
    public void Client_BeingServed_IsWhoTheCommandIsSignedAs()
    {
        var sessions = new ClientSessions(agent: "the-server");

        Assert.Equal("the-server", sessions.Current.Agent);

        using (sessions.Serve("claude-code", "lighting"))
        {
            Assert.Equal("lighting", sessions.Current.Agent);
        }

        Assert.Equal("the-server", sessions.Current.Agent);
    }

    /// <summary>A request that presented no key is the process's own client, which may do anything: what stdio has always been.</summary>
    [Fact]
    public void Request_WithoutAKey_IsTheProcesssOwnClient()
    {
        var sessions = new ClientSessions(agent: null);

        using (sessions.Serve("claude-code"))
        {
            Assert.Same(sessions.Default, sessions.Current);
            Assert.True(sessions.Current.May(Scopes.Python));
            Assert.Equal("claude-code", sessions.Current.Agent);
        }
    }

    /// <summary>A client that named no agent of its own is named after the client it says it is when it initializes.</summary>
    [Fact]
    public void Client_WithoutANameOfItsOwn_TakesTheNameItInitializedWith()
    {
        var session = new ClientSession("nameless", agent: null);

        session.NameItself("claude-code");
        session.NameItself("something-else");

        Assert.Equal("claude-code", session.Agent);
    }
}
