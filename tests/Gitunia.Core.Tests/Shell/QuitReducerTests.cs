using Gitunia.Core.Effects;
using Gitunia.Core.Shell;
using Gitunia.Core.Status;
using Xunit;

namespace Gitunia.Core.Tests.Shell;

public class QuitReducerTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void GivenNoUnsentMessage_WhenQuitting_ThenItLeavesAtOnce(bool force)
    {
        var update = AppReducer.Reduce(AppState.Empty, new QuitRequested(false, force));

        Assert.Equal(force, Assert.Single(update.Effects.OfType<QuitEffect>()).CancelWork);
        Assert.Empty(update.Effects.OfType<FlushSessionEffect>());
    }

    [Fact]
    public void GivenUnsentMessage_WhenQuitting_ThenTheSessionIsSavedFirst()
    {
        var update = AppReducer.Reduce(AppState.Empty, new QuitRequested(true, true));

        Assert.True(Assert.Single(update.Effects.OfType<FlushSessionEffect>()).Force);
        Assert.Empty(update.Effects.OfType<QuitEffect>());
    }

    [Fact]
    public void GivenTheSessionWasSaved_WhenTheAnswerArrives_ThenItLeaves()
    {
        var update = AppReducer.Reduce(AppState.Empty, new SessionFlushed(true, true));

        Assert.True(Assert.Single(update.Effects.OfType<QuitEffect>()).CancelWork);
        Assert.False(update.State.Shell.QuitWarned);
    }

    [Fact]
    public void GivenUnsentMessageAndFailedSave_WhenQuittingTwice_ThenTheFirstIsHeldBack()
    {
        var first = AppReducer.Reduce(AppState.Empty, new SessionFlushed(false, false));

        Assert.True(first.State.Shell.QuitWarned);
        Assert.Empty(first.Effects.OfType<QuitEffect>());
        var warning = Assert.Single(first.Effects.OfType<PostStatusEffect>());
        Assert.Equal(QuitReducer.WarningText, warning.Text);
        Assert.Equal(StatusSeverity.Warning, warning.Severity);

        var second = AppReducer.Reduce(first.State, new QuitRequested(true, false));

        Assert.Single(second.Effects.OfType<QuitEffect>());
        Assert.Empty(second.Effects.OfType<FlushSessionEffect>());
    }

    [Fact]
    public void GivenWarning_WhenTheDraftChangesAndSaveStillFails_ThenItWarnsAgain()
    {
        var warned = AppReducer.Reduce(AppState.Empty, new SessionFlushed(false, false)).State;

        var reset = AppReducer.Reduce(warned, new QuitWarningReset()).State;
        var update = AppReducer.Reduce(reset, new QuitRequested(true, false));

        Assert.False(reset.Shell.QuitWarned);
        Assert.Single(update.Effects.OfType<FlushSessionEffect>());
    }
}
