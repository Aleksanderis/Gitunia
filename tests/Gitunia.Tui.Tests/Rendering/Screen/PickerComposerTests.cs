using System.Collections.Immutable;
using Gitunia.Core.Graph;
using Gitunia.Core.Mouse;
using Gitunia.Core.Picker;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Tui.Rendering.Graph;
using Gitunia.Tui.Rendering.Screen;
using Gitunia.Tui.Rendering.Styling;
using Xunit;

namespace Gitunia.Tui.Tests.Rendering.Screen;

public class PickerComposerTests
{
    private static readonly string Home = OperatingSystem.IsWindows() ? @"C:\h" : Path.Combine(Path.GetTempPath(), "composer-home");

    private static PickerRow Row(string name, PickerTags tags) => new(Path.Combine(Home, "code", name), name, tags);

    private static string Frame(PickerState picker, int width, int height) =>
        string.Join('\n', PickerComposer.Compose(picker, Home, width, height, GraphGlyphSet.Unicode).Select(line => line.Text.TrimEnd()));

    private static string Sep(string path) => path.Replace('/', Path.DirectorySeparatorChar);

    // Windows shows native paths without the ~ shortcut, elsewhere a path below home starts with ~.
    private static string Shown(string relative) =>
        OperatingSystem.IsWindows() ? Path.Combine(Home, Sep(relative)) : Sep($"~/{relative}");

    [Fact]
    public void GivenRowsOfEverySource_WhenComposing_ThenPromptHintAndRowsWithTagsAndHomeAbbreviation()
    {
        var picker = new PickerState(
            string.Empty,
            1,
            [
                Row("alpha", PickerTags.Open | PickerTags.Recent),
                Row("beta", PickerTags.Pinned),
                Row("gamma", PickerTags.Recent),
                Row("delta", PickerTags.Discovered),
            ],
            string.Empty,
            null);

        var frame = Frame(picker, 70, 8);

        Assert.Equal(
            string.Join(
                '\n',
                " Open repository: _",
                $" type to filter, or a path starting with {PathDisplay.PathStartsHint(OperatingSystem.IsWindows())} (Tab accepts)",
                $" alpha  open            {Shown("code/alpha")}",
                $"{$" beta   pinned          {Shown("code/beta")}".PadRight(62)}[unpin]",
                $" gamma  recent          {Shown("code/gamma")}",
                $" delta  found           {Shown("code/delta")}",
                string.Empty,
                string.Empty),
            frame);
    }

    [Fact]
    public void GivenHighlight_WhenComposing_ThenOnlyThatRowIsSelected()
    {
        var picker = new PickerState("a", 1, [Row("alpha", PickerTags.Recent), Row("beta", PickerTags.Recent)], string.Empty, null);

        var lines = PickerComposer.Compose(picker, Home, 60, 6, GraphGlyphSet.Unicode);

        Assert.Equal([false, false, false, true], lines.Take(4).Select(line => line.IsSelected));
    }

    [Fact]
    public void GivenMessage_WhenComposing_ThenItIsShownBelowTheList()
    {
        var picker = new PickerState("~/x", 0, [], "not a repository: /x", null);

        var frame = Frame(picker, 50, 6).Split('\n');

        Assert.Equal(" no matching directories", frame[2]);
        Assert.Equal(" not a repository: /x", frame[3]);
    }

    [Fact]
    public void GivenHighlightBelowTheVisibleRows_WhenComposing_ThenTheListScrollsToShowIt()
    {
        var rows = Enumerable.Range(1, 10).Select(number => Row($"repo{number}", PickerTags.Recent)).ToImmutableArray();
        var picker = new PickerState(string.Empty, 7, rows, string.Empty, null);

        var lines = PickerComposer.Compose(picker, Home, 50, 6, GraphGlyphSet.Unicode);

        Assert.Contains("repo8", lines[^1].Text, StringComparison.Ordinal);
        Assert.True(lines[^1].IsSelected);
        Assert.DoesNotContain("repo1 ", lines[2].Text, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenLocatingMode_WhenComposing_ThenThePromptSaysLocate()
    {
        var picker = new PickerState(string.Empty, 0, [], string.Empty, new TabId(1));

        Assert.StartsWith(" Locate repository: _", Frame(picker, 50, 4), StringComparison.Ordinal);
    }

    [Fact]
    public void GivenPendingListing_WhenComposing_ThenScanningIsShown()
    {
        var picker = new PickerState("~/code/", 0, [], string.Empty, null, true, true);

        var lines = PickerComposer.Compose(picker, Home, 60, 6, GraphGlyphSet.Unicode);

        Assert.Contains("scanning...", lines[2].Text, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenDirectoryRows_WhenComposing_ThenRepositoriesAreMarked()
    {
        var picker = new PickerState("~/code/", 0, [Row("app", PickerTags.Directory | PickerTags.Repository), Row("docs", PickerTags.Directory)], string.Empty, null);

        var frame = Frame(picker, 60, 6).Split('\n');

        Assert.Contains("repository", frame[2], StringComparison.Ordinal);
        Assert.Contains("dir", frame[3], StringComparison.Ordinal);
    }

    [Fact]
    public void GivenOpenPickerInTheScreenModel_WhenComposingTheScreen_ThenItReplacesTheGraphAndStatusShowsPickerHints()
    {
        var picker = new PickerState(string.Empty, 0, [Row("alpha", PickerTags.Recent)], string.Empty, null);
        var model = new ScreenModel(string.Empty, null, 0, 0, "INSERT", string.Empty, 0, 0, false, string.Empty, Tabs: [], Picker: picker, HomeDirectory: Home);

        var lines = ScreenComposer.Compose(model, new CommitGraph(), 0, 0, 80, 8, GraphGlyphSet.Unicode);

        Assert.Equal(8, lines.Count);
        Assert.StartsWith(" no repository open", lines[0].Text, StringComparison.Ordinal);
        Assert.StartsWith("╭", lines[1].Text, StringComparison.Ordinal);
        Assert.StartsWith("│ Open repository: _", lines[2].Text, StringComparison.Ordinal);
        Assert.Contains("alpha", lines[4].Text, StringComparison.Ordinal);
        Assert.Contains("Alt-p pin", lines[^1].Text, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenARecentRowHighlighted_WhenComposing_ThenItShowsPinAndRemoveButtonsAtTheRightEdge()
    {
        var picker = new PickerState(string.Empty, 0, [Row("alpha", PickerTags.Recent), Row("beta", PickerTags.Recent)], string.Empty, null);

        var lines = PickerComposer.Compose(picker, Home, 80, 6, GraphGlyphSet.Unicode);

        Assert.EndsWith("[pin] [remove] ", lines[2].Text, StringComparison.Ordinal);
        Assert.DoesNotContain("[pin]", lines[3].Text, StringComparison.Ordinal);
        Assert.Equal(80, lines[2].Text.Length);
    }

    [Fact]
    public void GivenAPinnedRowHighlighted_WhenLayingOutTheButtons_ThenItOffersUnpinOnly()
    {
        var buttons = PickerRowButtons.Layout(Row("beta", PickerTags.Pinned), 80);

        var button = Assert.Single(buttons);
        Assert.Equal("[unpin]", button.Label);
        Assert.Equal(DefaultMouseMap.PickerPinButton, TitleButton.ViewAt(buttons, button.Start));
        Assert.Null(TitleButton.ViewAt(buttons, button.Start - 1));
    }

    [Fact]
    public void GivenADirectoryOrANarrowScreen_WhenLayingOutTheButtons_ThenThereAreNone()
    {
        Assert.Empty(PickerRowButtons.Layout(Row("docs", PickerTags.Directory), 80));
        Assert.Empty(PickerRowButtons.Layout(Row("alpha", PickerTags.Recent), PickerRowButtons.MinimumWidth - 1));
    }

    [Fact]
    public void GivenARecentRow_WhenLayingOutTheButtons_ThenRemoveSitsRightOfPin()
    {
        var buttons = PickerRowButtons.Layout(Row("alpha", PickerTags.Recent), 80);

        Assert.Equal([DefaultMouseMap.PickerPinButton, DefaultMouseMap.PickerRemoveButton], buttons.Select(button => button.View));
        Assert.True(buttons[1].Start > buttons[0].Start + buttons[0].Width);
    }

    [Fact]
    public void GivenAWideScreen_WhenComposing_ThenTheHintLineEndsWithCloneAndInitButtons()
    {
        var picker = new PickerState(string.Empty, 0, [], string.Empty, null);

        var lines = PickerComposer.Compose(picker, Home, 100, 6, GraphGlyphSet.Unicode);
        var buttons = PickerHeaderButtons.Layout(100);

        Assert.EndsWith("[clone] [init] ", lines[PickerHeaderButtons.Row].Text, StringComparison.Ordinal);
        Assert.Equal(100, lines[PickerHeaderButtons.Row].Text.Length);
        Assert.Equal(
            [DefaultMouseMap.PickerCloneButton, DefaultMouseMap.PickerInitButton],
            buttons.Select(button => button.View));
        Assert.Equal(DefaultMouseMap.PickerCloneButton, TitleButton.ViewAt(buttons, buttons[0].Start));
        Assert.Equal(DefaultMouseMap.PickerInitButton, TitleButton.ViewAt(buttons, buttons[1].Start));
        Assert.Null(TitleButton.ViewAt(buttons, buttons[0].Start - 1));
    }

    [Fact]
    public void GivenANarrowScreenOrTheLocateMode_WhenComposing_ThenThereAreNoCloneAndInitButtons()
    {
        var picker = new PickerState(string.Empty, 0, [], string.Empty, null);
        var locating = picker with { LocatingTabId = new TabId(1) };

        var narrowWidth = PickerHeaderButtons.MinimumWidth - 1;
        var narrow = PickerComposer.Compose(picker, Home, narrowWidth, 6, GraphGlyphSet.Unicode);
        var locate = PickerComposer.Compose(locating, Home, 100, 6, GraphGlyphSet.Unicode);

        Assert.Empty(PickerHeaderButtons.Layout(narrowWidth));
        Assert.DoesNotContain("[clone]", narrow[PickerHeaderButtons.Row].Text, StringComparison.Ordinal);
        Assert.DoesNotContain("[clone]", locate[PickerHeaderButtons.Row].Text, StringComparison.Ordinal);
    }
}
