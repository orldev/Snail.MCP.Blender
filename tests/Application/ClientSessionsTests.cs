using System.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Server;
using Snail.MCP.Blender.Application.Sessions;
using Snail.MCP.Blender.Application.Skills;

namespace Snail.MCP.Blender.Tests.Application;

/// <summary>One Blender, several clients: what one of them loads, signs and expires is its own.</summary>
public class ClientSessionsTests
{
    [Fact]
    public void Skill_LoadedByOneClient_IsNotInAnotherClientsToolList()
    {
        var sessions = Build();
        var lighting = sessions.Open(new McpServerOptions(), "lighting");
        var modeling = sessions.Open(new McpServerOptions(), "modeling");

        lighting.Skills.Enable("sessions");

        Assert.Contains("blender_sessions_probe", lighting.Skills.Tools.PrimitiveNames);
        Assert.DoesNotContain("blender_sessions_probe", modeling.Skills.Tools.PrimitiveNames);
        Assert.DoesNotContain("blender_sessions_probe", sessions.Default.Skills.Tools.PrimitiveNames);
    }

    /// <summary>A client of the sessionless revision is handed new options on every request; without a name of its own the skill it loaded would
    /// be gone by the next one.</summary>
    [Fact]
    public void Client_ThatNamesItself_IsTheSameSessionOnItsNextRequest()
    {
        var sessions = Build();
        var first = sessions.Open(new McpServerOptions(), "lighting");

        first.Skills.Enable("sessions");

        var options = new McpServerOptions();
        var second = sessions.Open(options, "  lighting ");

        Assert.Same(first, second);
        Assert.Contains("blender_sessions_probe", options.ToolCollection!.PrimitiveNames);
    }

    /// <summary>A client that says nothing cannot be told from any other, so it is served the tool list of the server itself.</summary>
    [Fact]
    public void Client_ThatNamesNothing_IsServedTheServersOwnToolList()
    {
        var sessions = Build();
        var options = new McpServerOptions();

        var anonymous = sessions.Open(options, agent: null);

        Assert.Same(sessions.Default, anonymous);
        Assert.Same(sessions.Default.Skills.Tools, options.ToolCollection);
    }

    /// <summary>Names arrive with requests, so the number of sessions they can open is bounded; past the bound a client shares the server's tool
    /// list but keeps the scopes of its own key.</summary>
    /// <remarks>It used to be handed the server's whole session, which holds every scope: a key minted to read alone became a key that could do
    /// anything, because sixty-four other names had got there first.</remarks>
    [Fact]
    public void Clients_BeyondTheNamesAllowed_ShareTheToolList_ButNotTheScopes()
    {
        var sessions = Build();

        for (var opened = 0; opened < ClientSessions.MostNamedClients; opened++)
        {
            Assert.NotSame(sessions.Default, sessions.Open(new McpServerOptions(), $"client-{opened}"));
        }

        var late = sessions.Open(new McpServerOptions(), "one-too-many", [Scopes.Read]);

        Assert.NotSame(sessions.Default, late);
        Assert.Equal([Scopes.Read], late.Scopes);
        Assert.False(late.May(Scopes.Python));
        Assert.Same(sessions.Default.Skills.Tools, late.Skills.Tools);
        Assert.NotSame(sessions.Default, sessions.Open(new McpServerOptions(), "client-7"));
    }

    /// <summary>A key re-minted with narrower scopes is narrower at once: the session takes what the key it just admitted opens, rather than what
    /// the first key of that name opened.</summary>
    [Fact]
    public void Client_PresentingANarrowerKey_LosesWhatTheOldOneOpened()
    {
        var sessions = Build();

        sessions.Open(new McpServerOptions(), "lighting", [Scopes.Read, Scopes.Write, Scopes.Python]);
        var narrowed = sessions.Open(new McpServerOptions(), "lighting", [Scopes.Read]);

        Assert.Equal([Scopes.Read], narrowed.Scopes);
        Assert.False(narrowed.May(Scopes.Python));
    }

    /// <summary>The agent the add-on records is the client being served, not the one the server was started with.</summary>
    [Fact]
    public void Client_BeingServed_IsWhoTheCommandIsSignedAs()
    {
        var sessions = Build(agent: "the-server");
        var lighting = sessions.Open(new McpServerOptions(), "lighting");

        Assert.Equal("the-server", sessions.Current.Agent);

        using (sessions.Serve(lighting))
        {
            Assert.Equal("lighting", sessions.Current.Agent);
        }

        Assert.Equal("the-server", sessions.Current.Agent);
    }

    /// <summary>A client that named no agent of its own is named after the client it says it is when it initializes.</summary>
    [Fact]
    public void Client_WithoutANameOfItsOwn_TakesTheNameItInitializedWith()
    {
        var session = new ClientSession("nameless", agent: null, new SkillCatalog([], [], new ServiceCollection().BuildServiceProvider(), TimeProvider.System));

        session.NameItself("claude-code");
        session.NameItself("something-else");

        Assert.Equal("claude-code", session.Agent);
    }

    private static ClientSessions Build(string? agent = null) =>
        new(SkillCatalog.Discover(typeof(ClientSessionsTests).Assembly),
            [],
            agent,
            new ServiceCollection().BuildServiceProvider(),
            TimeProvider.System);

    [McpServerToolType]
    [Skill("sessions", "A skill of this test alone.")]
    public sealed class ProbeTools
    {
        [McpServerTool(Name = "blender_sessions_probe")]
        [Description("one")]
        public string One() => "one";
    }
}
