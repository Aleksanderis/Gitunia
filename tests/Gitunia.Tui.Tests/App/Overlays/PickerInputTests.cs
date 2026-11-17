using Gitunia.App;
using Gitunia.Core;
using Gitunia.Core.Picker;
using Gitunia.Core.Shell;
using Gitunia.Core.Workspaces;
using Gitunia.Tui.App.Overlays;
using Xunit;

namespace Gitunia.Tui.Tests.App.Overlays;

public class PickerInputTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void GivenAClickOnAMoreLine_WhenClicking_ThenTheHighlightSitsOnTheMoreLineAndNothingIsOpened()
    {
        var (host, input) = Opened();
        var more = MoreLine(input);

        input.ClickRow(more);

        Assert.True(input.State!.IsOnToggle);
        Assert.Equal(more.RowIndex, input.State.Highlight);
        Assert.Null(input.State.HighlightedRow);
        Assert.Equal(string.Empty, input.State.Message);
        Assert.True(host.State.Shell.Picker.IsOpen);
    }

    [Fact]
    public void GivenAClickOnAMoreLineThenTheAcceptOfADoubleClick_WhenDispatched_ThenTheSectionExpandsAndLessCollapsesIt()
    {
        var (host, input) = Opened();
        input.ClickRow(MoreLine(input));

        host.Dispatch(new PickerAcceptPressed());

        Assert.Equal(PickerSection.Recent, input.State!.Expanded);
        var entries = PickerListPlan.Build(input.State, PickerListPlan.MaximumListRows);
        var less = entries.Single(entry => entry.Kind == PickerEntryKind.Less);
        input.ClickRow(less);
        host.Dispatch(new PickerAcceptPressed());
        Assert.Equal(PickerSection.None, input.State.Expanded);
    }

    [Fact]
    public void GivenAClickOnARow_WhenClicking_ThenTheHighlightSitsOnThatRow()
    {
        var (_, input) = Opened();
        var entries = PickerListPlan.Build(input.State!, PickerListPlan.MaximumListRows);

        input.ClickRow(entries.First(entry => entry.Kind == PickerEntryKind.Row && entry.RowIndex == 2));

        Assert.False(input.State!.IsOnToggle);
        Assert.Equal(2, input.State.Highlight);
    }

    private static PickerListEntry MoreLine(PickerInput input) =>
        PickerListPlan.Build(input.State!, PickerListPlan.MaximumListRows)
            .Single(entry => entry.Kind == PickerEntryKind.More);

    private static (Dispatcher Host, PickerInput Input) Opened()
    {
        var home = Path.Combine(Path.GetPathRoot(Path.GetTempPath())!, "picker-input-tests", "home");
        var recents = Enumerable.Range(1, 12).Select(number => Path.Combine(home, "code", $"recent{number:00}"));
        var state = new AppState(
            Workspace.Empty with { Recents = [.. recents] },
            ShellState.Empty with { Picker = PickerSession.Empty with { HomeDirectory = home } });
        var host = new Dispatcher(state);
        host.Dispatch(new PickerOpenRequested(null, string.Empty, Now));

        return (host, new PickerInput(host));
    }
}
