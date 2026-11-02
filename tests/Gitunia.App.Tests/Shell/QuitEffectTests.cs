using Gitunia.App.Tests.Picker;
using Gitunia.Core.Shell;
using Gitunia.Core.Status;
using Xunit;

namespace Gitunia.App.Tests.Shell;

// The quit guard through the real runner: the terminal layer only saves the session and stops the loop.
public class QuitEffectTests
{
    [Fact]
    public void GivenNoUnsentMessage_WhenQuitting_ThenTheLoopStopsWithoutSaving()
    {
        using var pipeline = PickerPipeline.Create();

        pipeline.Dispatch(new QuitRequested(false, false));

        Assert.Equal(1, pipeline.Shell.Quits);
        Assert.Equal(0, pipeline.Shell.Flushed);
    }

    [Fact]
    public void GivenUnsentMessageAndASuccessfulSave_WhenQuitting_ThenTheSessionIsSavedAndTheLoopStops()
    {
        using var pipeline = PickerPipeline.Create();

        pipeline.Dispatch(new QuitRequested(true, false));

        Assert.Equal(1, pipeline.Shell.Flushed);
        Assert.Equal(1, pipeline.Shell.Quits);
    }

    [Fact]
    public async Task GivenUnsentMessageAndFailedSave_WhenQuittingTwice_ThenTheFirstIsHeldBackAndTheSecondLeaves()
    {
        using var pipeline = PickerPipeline.Create();
        pipeline.Shell.FlushSucceeds = false;

        pipeline.Dispatch(new QuitRequested(true, false));
        await pipeline.Pump.RunUntilAsync(() => pipeline.Statuses.Log.Latest is not null);

        Assert.Equal(0, pipeline.Shell.Quits);
        Assert.Equal(QuitReducer.WarningText, pipeline.Statuses.Log.Latest!.Text);
        Assert.Equal(StatusSeverity.Warning, pipeline.Statuses.Log.Latest.Severity);

        pipeline.Dispatch(new QuitRequested(true, false));

        Assert.Equal(1, pipeline.Shell.Quits);
        Assert.Equal(1, pipeline.Shell.Flushed);
    }
}
