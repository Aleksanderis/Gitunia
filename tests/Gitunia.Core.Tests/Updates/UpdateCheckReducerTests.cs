using Gitunia.Core.Shell;
using Gitunia.Core.Updates;
using Xunit;

namespace Gitunia.Core.Tests.Updates;

[Trait("Os", "Independent")]
public class UpdateCheckReducerTests
{
    private static readonly UpdateNotice Notice = new("0.9.0", "gitunia update");

    [Fact]
    public void GivenAnAllowedCheckOnAReleaseBuild_WhenRequested_ThenTheCheckStarts()
    {
        var update = Reduce(ShellState.Empty, new UpdateCheckRequested("0.8.0", true));

        Assert.True(update.State.Update.Allowed);
        Assert.Equal("0.8.0", Assert.IsType<CheckForUpdateEffect>(Assert.Single(update.Effects)).CurrentVersion);
    }

    [Fact]
    public void GivenTheSettingOff_WhenRequested_ThenNoCheckStarts()
    {
        var update = Reduce(ShellState.Empty, new UpdateCheckRequested("0.8.0", false));

        Assert.Empty(update.Effects);
        Assert.False(update.State.Update.Allowed);
    }

    [Theory]
    [InlineData("0.0.0-dev")]
    [InlineData("not a version")]
    [InlineData("")]
    public void GivenADevelopmentBuildOrNoVersion_WhenRequested_ThenNoCheckStarts(string version)
    {
        var update = Reduce(ShellState.Empty, new UpdateCheckRequested(version, true));

        Assert.Empty(update.Effects);
    }

    [Fact]
    public void GivenAnAllowedCheck_WhenItFindsAnUpdate_ThenTheNoticeIsKept()
    {
        var allowed = Reduce(ShellState.Empty, new UpdateCheckRequested("0.8.0", true)).State;

        var update = Reduce(allowed, new UpdateCheckCompleted(Notice));

        Assert.Equal(Notice, update.State.Update.Notice);
    }

    [Fact]
    public void GivenAShownNotice_WhenTheSettingIsTurnedOff_ThenTheNoticeIsCleared()
    {
        var shown = Reduce(Reduce(ShellState.Empty, new UpdateCheckRequested("0.8.0", true)).State, new UpdateCheckCompleted(Notice));

        var update = Reduce(shown.State, new UpdateCheckRequested("0.8.0", false));

        Assert.Null(update.State.Update.Notice);
    }

    [Fact]
    public void GivenTheSettingTurnedOffWhileTheCheckRuns_WhenTheResultArrives_ThenItIsDropped()
    {
        var running = Reduce(ShellState.Empty, new UpdateCheckRequested("0.8.0", true)).State;
        var off = Reduce(running, new UpdateCheckRequested("0.8.0", false)).State;

        var update = Reduce(off, new UpdateCheckCompleted(Notice));

        Assert.Null(update.State.Update.Notice);
    }

    [Fact]
    public void GivenAShownNotice_WhenACheckFindsNothing_ThenTheNoticeIsCleared()
    {
        var shown = Reduce(Reduce(ShellState.Empty, new UpdateCheckRequested("0.8.0", true)).State, new UpdateCheckCompleted(Notice));

        var update = Reduce(shown.State, new UpdateCheckCompleted(null));

        Assert.Null(update.State.Update.Notice);
    }

    private static ShellUpdate Reduce(ShellState state, UpdateCheckCommand command) =>
        ShellReducer.Reduce(state, command);
}
