using Gitunia.Core.Editing;
using Xunit;

namespace Gitunia.Core.Tests.Editing;

public class CommitDraftTests
{
    [Fact]
    public void GivenAnEmptyDraft_WhenAmendingAndHeadsMessageArrives_ThenTheBoxIsFilledAndTheTitleNamesTheCommit()
    {
        var draft = new CommitDraft();

        draft.SetAmend(true);
        draft.ApplyHeadMessage("abc1234", "Old subject\n\nOld body");

        Assert.Equal("Old subject\n\nOld body", draft.Editor.Text);
        Assert.Equal("Amending 11/72 abc1234 Old subject", draft.Title);
    }

    [Fact]
    public void GivenAMessage_WhenReadingTheTitle_ThenItEndsWithTheSubjectWidthAndTheSoftLimit()
    {
        var draft = new CommitDraft();
        Assert.Equal("Commit message", draft.Title);

        draft.Editor.SetText($"中文 x\nbody that is not counted");

        Assert.Equal("Commit message 6/72", draft.Title);
    }

    [Fact]
    public void GivenAFilledBox_WhenAmendIsSwitchedOff_ThenTheEmptyDraftComesBack()
    {
        var draft = new CommitDraft();
        draft.SetAmend(true);
        draft.ApplyHeadMessage("abc1234", "Old subject");

        draft.SetAmend(false);

        Assert.True(draft.Editor.IsBlank);
        Assert.Equal("Commit message", draft.Title);
    }

    [Fact]
    public void GivenTheFilledMessageWasEdited_WhenAmendIsSwitchedOff_ThenTheEditIsKept()
    {
        var draft = new CommitDraft();
        draft.SetAmend(true);
        draft.ApplyHeadMessage("abc1234", "Old subject");
        draft.Editor.Insert(" reworded");

        draft.SetAmend(false);

        Assert.Equal("Old subject reworded", draft.Editor.Text);
    }

    [Fact]
    public void GivenATypedDraft_WhenAmendingAndHeadsMessageArrives_ThenTheDraftIsNotReplaced()
    {
        var draft = new CommitDraft();
        draft.Editor.SetText("My own message");
        draft.SetAmend(true);

        draft.ApplyHeadMessage("abc1234", "Old subject");

        Assert.Equal("My own message", draft.Editor.Text);
        Assert.Equal("Amending 14/72 abc1234 Old subject", draft.Title);
    }

    [Fact]
    public void GivenAmendWasSwitchedOffMeanwhile_WhenHeadsMessageArrives_ThenItIsIgnored()
    {
        var draft = new CommitDraft();
        draft.SetAmend(true);
        draft.SetAmend(false);

        draft.ApplyHeadMessage("abc1234", "Old subject");

        Assert.True(draft.Editor.IsBlank);
    }

    [Fact]
    public void GivenASentCommit_WhenClearing_ThenMessageAndAmendGoAndTheOptionsStay()
    {
        var draft = new CommitDraft { SignOff = true, NoVerify = true };
        draft.Editor.SetText("Message");
        draft.SetAmend(true);
        draft.Top = 3;

        draft.Sent();

        Assert.True(draft.Editor.IsBlank);
        Assert.False(draft.Amend);
        Assert.True(draft.SignOff);
        Assert.True(draft.NoVerify);
        Assert.Equal(0, draft.Top);
        Assert.False(draft.HasUnsentText);
    }

    [Fact]
    public void GivenAPrefilledMessage_WhenAskingForUnsentText_ThenItCountsOnlyOnceItIsEdited()
    {
        var draft = new CommitDraft();
        draft.SetAmend(true);
        draft.ApplyHeadMessage("abc1234", "Old subject");
        Assert.False(draft.HasUnsentText);

        draft.Editor.Insert("!");

        Assert.True(draft.HasUnsentText);

        var merge = new CommitDraft();
        merge.Prefill("Merge branch 'x'");
        Assert.False(merge.HasUnsentText);
        merge.Prefill("ignored: not empty");
        Assert.Equal("Merge branch 'x'", merge.Editor.Text);
    }

    [Fact]
    public void GivenOnlyWhitespace_WhenAskingForUnsentText_ThenThereIsNone()
    {
        var draft = new CommitDraft();
        draft.Editor.SetText("  \n ");

        Assert.False(draft.HasUnsentText);
    }
}
