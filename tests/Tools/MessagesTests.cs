using Snail.MCP.Blender.Tools.Prompts;

namespace Snail.MCP.Blender.Tests.Tools;

/// <summary>The hint that follows a failure: every kind the add-on and the link can answer with has one, and the two that depend on what Blender was doing say different things.</summary>
public class MessagesTests
{
    public static TheoryData<string> Kinds() =>
    [
        BridgeError.UnavailableType,
        BridgeError.TimeoutType,
        BridgeError.DroppedType,
        BridgeError.MalformedType,
        BridgeError.BusyType,
        BridgeError.TokenUnreadableType,
        "UnknownOperator",
        "UnknownParameter",
        "PollFailed",
        "NoArea",
        "Unauthorized",
        "UnknownCommand",
    ];

    [Theory]
    [MemberData(nameof(Kinds))]
    public void EveryKindOfFailure_CarriesAHint(string kind)
    {
        var hint = Messages.HintFor(new BridgeError(kind, "something went wrong"));

        Assert.False(string.IsNullOrWhiteSpace(hint), $"a failure of kind '{kind}' leaves the model without a next step");
    }

    /// <summary>A hint invented for a kind the server has nothing to say about would be read as advice about a failure that did not happen.</summary>
    [Fact]
    public void AFailureTheServerHasNoAdviceFor_CarriesNoHint()
    {
        Assert.Null(Messages.HintFor(RecordedAnswers.Error("a primitive of a kind there is none of")));
    }

    /// <summary>A timeout on a render is not a timeout on a question: one says the work goes on inside Blender, the other says Blender is busy.</summary>
    [Fact]
    public void ATimeoutOnARender_PointsAtTheJobTools_WhereAPlainTimeoutPointsAtBlender()
    {
        var render = Messages.HintFor(BridgeError.Timeout(BridgeCommands.RenderImage, TimeSpan.FromSeconds(30)));
        var question = Messages.HintFor(BridgeError.Timeout(BridgeCommands.SceneInfo, TimeSpan.FromSeconds(30)));

        Assert.Equal(Messages.RenderTimeoutHint, render);
        Assert.Equal(Messages.TimeoutHint, question);
    }

    [Fact]
    public void WhatBlenderIsBusyWith_SaysWhetherWaitingIsTheOnlyOption()
    {
        Assert.Contains("cannot be interrupted", Messages.BusyWith(BridgeCommands.RenderImage.Name), StringComparison.Ordinal);
        Assert.DoesNotContain("cannot be interrupted", Messages.BusyWith(BridgeCommands.SceneInfo.Name), StringComparison.Ordinal);
    }

    [Fact]
    public void ThePageCountsWhatItDeleted_InSingularAndInPlural()
    {
        Assert.Equal("Deleted one item, 2 MB freed.", Messages.DeletedFromThePage(1, "2 MB"));
        Assert.Equal("Deleted 3 items, 2 MB freed.", Messages.DeletedFromThePage(3, "2 MB"));
        Assert.Contains("job-1", Messages.CancelledFromThePage("job-1"), StringComparison.Ordinal);
    }

    [Fact]
    public void AnUnknownSkill_IsNamed_AndTheKnownOnesAreListed()
    {
        Assert.Contains("'sculpting'", Messages.UnknownSkill("sculpting"), StringComparison.Ordinal);
        Assert.Contains("modeling, rendering", Messages.KnownSkills(["modeling", "rendering"]), StringComparison.Ordinal);
    }
}
