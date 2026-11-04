using Gitunia.Tui.Rendering.Overlays;
using Gitunia.App;
using Gitunia.App.Blame;
using Gitunia.App.Status;
using Gitunia.Core;
using Gitunia.Core.Blame;
using Gitunia.Core.Visual;
using Gitunia.Core.Clipboard;
using Gitunia.Core.Effects;
using Gitunia.Core.FileHistory;
using Gitunia.Git.History.Models;
using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Mouse;
using Gitunia.Core.Shell;
using Gitunia.Core.Sidebar;
using Gitunia.Core.Workspaces;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Git.Tests.Fixtures;
using Gitunia.Tui.App.Blame;
using Gitunia.Tui.Rendering.Graph;
using Gitunia.Platform;
using Gitunia.Tui.App.Clipboard;
using Gitunia.Tui.App.Config;
using Gitunia.Tui.App.Panes;
using Gitunia.Tui.Input;
using Gitunia.Tui.Tests.App.Panes;
using Gitunia.Tui.Tests.Fakes;
using Terminal.Gui.Views;
using Xunit;

namespace Gitunia.Tui.Tests.App.Blame;

// The blame view through the real path: the input class, the reducer, the effect handler and real git.
public sealed class BlameInputTests
{
    private const int Width = 100;
    private const int Height = 30;

    [Fact]
    public async Task GivenTwoCommits_WhenOpeningTheBlame_ThenTheGutterShowsIdAuthorAgeAndTheLine()
    {
        using var repository = TwoCommits();
        using var harness = await Harness.LoadAsync(repository, new FixedTargets("a.txt", repository.Head));

        await harness.OpenAsync();

        var frame = harness.Input.CurrentFrame()!;
        var first = frame.Lines.Single(line => line.Text.Contains("one"));
        var second = frame.Lines.Single(line => line.Text.Contains("TWO"));
        Assert.Equal(Height - 1, frame.Height);
        Assert.Contains("Blame: a.txt @", frame.Lines[0].Text);
        Assert.Contains($"^{repository.Git("rev-parse", "--short=7", "HEAD~1").Trim()}", first.Text);
        Assert.Contains(repository.Head[..7], second.Text);
        Assert.Contains("ago", second.Text);
        Assert.Contains("|", second.Text);
        Assert.Contains("1/3", frame.Lines[^2].Text);
    }

    [Fact]
    public async Task GivenAnEditedWorkingTreeFile_WhenOpeningTheBlame_ThenTheNewLineShowsAsUncommitted()
    {
        using var repository = TwoCommits();
        repository.WriteFile("a.txt", "one\nTWO\nthree\nextra\n");
        using var harness = await Harness.LoadAsync(repository, new FixedTargets("a.txt", null));

        await harness.OpenAsync();

        var frame = harness.Input.CurrentFrame()!;
        Assert.Contains("uncommitted", frame.Lines.Single(line => line.Text.Contains("extra")).Text);
        Assert.Contains("working tree", frame.Lines[0].Text);
    }

    [Fact]
    public async Task GivenALine_WhenPressingEnter_ThenTheViewClosesAndTheGraphIsAskedForThatLinesCommit()
    {
        using var repository = TwoCommits();
        var first = repository.Git("rev-parse", "HEAD~1").Trim();
        using var harness = await Harness.LoadAsync(repository, new FixedTargets("a.txt", repository.Head));
        await harness.OpenAsync();

        Assert.True(harness.Input.TryExecute(ActionIds.BlameShowCommit, null));
        var locate = Assert.Single(harness.Effects.OfType<LocateGraphCommitEffect>());

        Assert.False(harness.Input.IsOpen);
        Assert.Equal(first, locate.Commit.ToString());
        Assert.Null(harness.Input.CurrentFrame());
    }

    [Fact]
    public async Task GivenALineOfTheSecondCommit_WhenOpeningItsLineHistory_ThenTheViewClosesAndTheWalkStartsAtThatCommit()
    {
        using var repository = TwoCommits();
        using var harness = await Harness.LoadAsync(repository, new FixedTargets("a.txt", repository.Head));
        await harness.OpenAsync();

        harness.Input.TryExecute(ActionIds.ListDown, null);
        Assert.True(harness.Input.TryExecute(ActionIds.LineHistoryOpen, null));

        var read = Assert.Single(harness.Effects.OfType<ReadFileHistoryEffect>());
        Assert.False(harness.Input.IsOpen);
        Assert.Equal(repository.Head, read.Revision);
        Assert.Equal("a.txt", read.Path);
        Assert.Equal(new LineRange(2, 2), read.Range);
    }

    [Fact]
    public async Task GivenAnEditedWorkingTreeFile_WhenOpeningTheLineHistory_ThenCommittedLinesStartAtTheirCommitAndNewOnesRefuse()
    {
        using var repository = TwoCommits();
        repository.WriteFile("a.txt", "new\none\nTWO\nthree\n");
        using var harness = await Harness.LoadAsync(repository, new FixedTargets("a.txt", null));
        await harness.OpenAsync();

        harness.Input.TryExecute(ActionIds.LineHistoryOpen, null);

        Assert.True(harness.Input.IsOpen);
        Assert.Empty(harness.Effects.OfType<ReadFileHistoryEffect>());
        Assert.Equal(BlameReducer.UncommittedHistoryNotice, harness.Statuses.Log.Latest!.Text);

        harness.Input.TryExecute(ActionIds.ListDown, null);
        harness.Input.TryExecute(ActionIds.LineHistoryOpen, null);

        var read = Assert.Single(harness.Effects.OfType<ReadFileHistoryEffect>());
        Assert.Equal(repository.Git("rev-parse", "HEAD~1").Trim(), read.Revision);
        Assert.Equal(new LineRange(1, 1), read.Range);
    }

    [Fact]
    public async Task GivenALineOfTheSecondCommit_WhenMovingDownAndPressingEnter_ThenThatCommitIsLocated()
    {
        using var repository = TwoCommits();
        using var harness = await Harness.LoadAsync(repository, new FixedTargets("a.txt", repository.Head));
        await harness.OpenAsync();

        harness.Input.TryExecute(ActionIds.ListDown, null);
        harness.Input.TryExecute(ActionIds.BlameShowCommit, null);

        Assert.Equal(repository.Head, Assert.Single(harness.Effects.OfType<LocateGraphCommitEffect>()).Commit.ToString());
    }

    [Fact]
    public async Task GivenTypedSearchText_WhenAccepting_ThenTheCursorLandsOnTheMatchingLine()
    {
        using var repository = TwoCommits();
        using var harness = await Harness.LoadAsync(repository, new FixedTargets("a.txt", repository.Head));
        await harness.OpenAsync();

        harness.Input.TryExecute(ActionIds.SearchOpen, null);
        Assert.True(harness.Input.HandleTextKey(new KeyEvent("t")));
        Assert.True(harness.Input.HandleTextKey(new KeyEvent("h")));
        harness.Input.TryExecute(ActionIds.InputAccept, null);
        await harness.PumpUntilAsync(() => harness.View.Cursor == 2);

        Assert.Equal(KeyMode.Normal, harness.Host.State.Shell.Mode);
        Assert.Contains("/th", harness.Input.CurrentFrame()!.Lines[^2].Text);
    }

    [Fact]
    public async Task GivenNothingSelected_WhenOpeningTheBlame_ThenTheStatusSaysSoAndNoViewOpens()
    {
        using var repository = TwoCommits();
        using var harness = await Harness.LoadAsync(repository, new FixedTargets(null, null));

        Assert.True(harness.Input.TryExecute(ActionIds.BlameOpen, null));

        Assert.False(harness.Input.IsOpen);
        Assert.Equal("nothing selected", harness.Statuses.Log.Latest!.Text);
    }

    [Fact]
    public async Task GivenALineOfTheSecondCommit_WhenBlamingAtTheParentAndGoingBack_ThenTheOlderFileShowsAndEscReturns()
    {
        using var repository = TwoCommits();
        using var harness = await Harness.LoadAsync(repository, new FixedTargets("a.txt", repository.Head));
        await harness.OpenAsync();
        harness.Input.TryExecute(ActionIds.ListDown, null);

        harness.Input.TryExecute(ActionIds.BlameParent, null);
        await harness.PumpUntilAsync(() => harness.View is { IsComplete: true, Revision: not null } view
            && view.Revision.EndsWith('^'));

        var frame = harness.Input.CurrentFrame()!;
        Assert.Contains($"@ {repository.Head[..7]}^", frame.Lines[0].Text);
        Assert.Equal(2, harness.View.LineCount);
        Assert.Contains(frame.Lines, line => line.Text.Contains("three"));
        Assert.DoesNotContain(frame.Lines, line => line.Text.Contains("TWO"));
        Assert.Equal(1, harness.View.Cursor);

        harness.Input.TryExecute(ActionIds.NavigateBack, null);
        await harness.PumpUntilAsync(() => harness.View is { IsComplete: true, Revision: { } revision }
            && revision == repository.Head);

        Assert.Equal(3, harness.View.LineCount);
        Assert.Contains(harness.Input.CurrentFrame()!.Lines, line => line.Text.Contains("TWO"));
        Assert.True(harness.Input.IsOpen);
    }

    [Fact]
    public async Task GivenALineEditedByARenameCommit_WhenBlamingAtTheParent_ThenTheOldPathIsBlamed()
    {
        using var repository = TestRepository.Create()
            .Commit("add", new TestFile("a.txt", "one\ntwo\nthree\nfour\n"));
        repository.Git("mv", "a.txt", "b.txt");
        repository.WriteFile("b.txt", "one\ntwo\nthree\nFOUR\n");
        repository.Git("commit", "-q", "-am", "rename and edit");
        using var harness = await Harness.LoadAsync(repository, new FixedTargets("b.txt", repository.Head));
        await harness.OpenAsync();
        harness.Input.TryExecute(ActionIds.ListBottom, null);

        harness.Input.TryExecute(ActionIds.BlameParent, null);
        await harness.PumpUntilAsync(() => harness.View is { IsComplete: true, Revision: not null } view
            && view.Revision.EndsWith('^'));

        Assert.Equal("a.txt", harness.View.Path);
        Assert.Equal(4, harness.View.LineCount);
        Assert.Contains(harness.Input.CurrentFrame()!.Lines, line => line.Text.Contains("four"));
    }

    [Fact]
    public async Task GivenALineOfTheFirstCommit_WhenBlamingAtTheParent_ThenTheStatusSaysThereIsNoParent()
    {
        using var repository = TwoCommits();
        using var harness = await Harness.LoadAsync(repository, new FixedTargets("a.txt", repository.Head));
        await harness.OpenAsync();

        harness.Input.TryExecute(ActionIds.BlameParent, null);

        Assert.Equal(BlameReducer.NoParentNotice, harness.Statuses.Log.Latest!.Text);
        Assert.Equal(repository.Head, harness.View.Revision);
    }

    [Fact]
    public async Task GivenWhitespaceAndIgnoreRevs_WhenBlaming_ThenTheFooterNamesTheSwitchesAndTheFileInUse()
    {
        using var repository = TwoCommits();
        repository.WriteFile("ignore-revs.txt", $"{repository.Head}\n");
        repository.Git("config", "blame.ignoreRevsFile", "ignore-revs.txt");
        using var harness = await Harness.LoadAsync(repository, new FixedTargets("a.txt", repository.Head));
        await harness.OpenAsync();

        harness.Input.TryExecute(ActionIds.BlameIgnoreWhitespace, null);
        await harness.PumpUntilAsync(() => harness.View is { IsComplete: true, Options.IgnoreWhitespace: true });

        var footer = harness.Input.CurrentFrame()!.Lines[^2].Text;
        Assert.Contains("-w", footer);
        Assert.Contains("ignore-revs", footer);
    }

    [Fact]
    public async Task GivenTheView_WhenClickingALineAndTheActionRow_ThenTheLineIsHighlightedAndTheRowNamesItsMouseView()
    {
        using var repository = TwoCommits();
        using var harness = await Harness.LoadAsync(repository, new FixedTargets("a.txt", repository.Head));
        await harness.OpenAsync();

        var onLine = harness.Input.OnLineClicked(3, 0, out var lineHandled);
        var onAction = harness.Input.OnLineClicked(BlameFrameBuilder.VisibleRows(Height) + 1, 0, out var actionHandled);
        var onParent = harness.Input.OnLineClicked(BlameFrameBuilder.VisibleRows(Height) + 2, 0, out var parentHandled);

        Assert.Null(onLine);
        Assert.True(lineHandled);
        Assert.Equal(2, harness.View.Cursor);
        Assert.Equal(DefaultMouseMap.BlameShowButton, onAction);
        Assert.True(actionHandled);
        Assert.Equal(DefaultMouseMap.BlameParentButton, onParent);
        Assert.True(parentHandled);
        var onHistory = harness.Input.OnLineClicked(BlameFrameBuilder.VisibleRows(Height) + 3, 0, out var historyHandled);
        Assert.Equal(DefaultMouseMap.BlameHistoryButton, onHistory);
        Assert.True(historyHandled);
        Assert.Equal(ActionIds.FileHistoryOpen, DefaultMouseMap.Create().Resolve(onHistory!, MouseGesture.Click)!.Action);
        var onLineHistory = harness.Input.OnLineClicked(BlameFrameBuilder.VisibleRows(Height) + 4, 0, out var lineHistoryHandled);
        Assert.Equal(DefaultMouseMap.BlameLineHistoryButton, onLineHistory);
        Assert.True(lineHistoryHandled);
        Assert.Equal(ActionIds.LineHistoryOpen, DefaultMouseMap.Create().Resolve(onLineHistory!, MouseGesture.Click)!.Action);
        Assert.Equal(ActionIds.BlameParent, DefaultMouseMap.Create().Resolve(onParent!, MouseGesture.Click)!.Action);
        Assert.Equal(ActionIds.BlameShowCommit, DefaultMouseMap.Create().Resolve(onAction!, MouseGesture.Click)!.Action);
    }

    [Fact]
    public async Task GivenALineSelection_WhenCopying_ThenTheClipboardGetsTheCodeOfTheLinesWithoutTheGutter()
    {
        using var repository = TwoCommits();
        using var harness = await Harness.LoadAsync(repository, new FixedTargets("a.txt", repository.Head));
        await harness.OpenAsync();

        harness.Input.TryExecute(ActionIds.VisualToggle, null);
        harness.Input.TryExecute(ActionIds.ListDown, null);
        harness.Input.TryExecute(ActionIds.VisualCopy, null);

        var copy = Assert.Single(harness.Copies);
        Assert.Equal("one\nTWO", copy.Text);
        Assert.Equal("2 lines of code", copy.Description);
        Assert.Null(harness.View.Selection);
    }

    [Fact]
    public async Task GivenACharacterSelectionAcrossLines_WhenCopying_ThenExactlyTheSelectedCharactersAreCopied()
    {
        using var repository = TwoCommits();
        using var harness = await Harness.LoadAsync(repository, new FixedTargets("a.txt", repository.Head));
        await harness.OpenAsync();

        harness.Input.TryExecute(ActionIds.VisualToggleChars, null);
        harness.Input.TryExecute(ActionIds.VisualCharRight, null);
        harness.Input.TryExecute(ActionIds.ListDown, null);
        harness.Input.TryExecute(ActionIds.VisualCopy, null);

        var copy = Assert.Single(harness.Copies);
        Assert.Equal("one\nTW", copy.Text);
        Assert.Equal("6 selected characters", copy.Description);
    }

    [Fact]
    public async Task GivenTheWordMotions_WhenSelectingWithIwAndIl_ThenTheWordAndTheLineAreCopied()
    {
        using var repository = TestRepository.Create().Commit("first", new TestFile("a.txt", "foo bar baz\n"));
        using var harness = await Harness.LoadAsync(repository, new FixedTargets("a.txt", repository.Head));
        await harness.OpenAsync();

        harness.Input.TryExecute(ActionIds.VisualToggleChars, null);
        harness.Input.TryExecute(ActionIds.VisualWordNext, null);
        harness.Input.TryExecute(ActionIds.VisualSelectWord, null);
        harness.Input.TryExecute(ActionIds.VisualCopy, null);
        harness.Input.TryExecute(ActionIds.VisualToggleChars, null);
        harness.Input.TryExecute(ActionIds.VisualSelectLine, null);
        harness.Input.TryExecute(ActionIds.VisualCopy, null);

        Assert.Equal(["bar", "foo bar baz"], harness.Copies.Select(copy => copy.Text));
    }

    [Fact]
    public async Task GivenADragFromTheCode_WhenCopying_ThenTheCharactersUnderThePointerAreCopiedTabsIncluded()
    {
        using var repository = TestRepository.Create().Commit("first", new TestFile("a.txt", "ab\tcd\nsecond\n"));
        using var harness = await Harness.LoadAsync(repository, new FixedTargets("a.txt", repository.Head));
        await harness.OpenAsync();
        var code = BlameFrameBuilder.CodeStart(Width);

        harness.Input.OnDragStarted(1, code + 1);
        harness.Input.OnDragged(2, code + 2);
        harness.Input.TryExecute(ActionIds.VisualCopy, null);

        Assert.Equal("b\tcd\nsec", Assert.Single(harness.Copies).Text);
    }

    [Fact]
    public async Task GivenADragFromTheGutter_WhenCopying_ThenWholeLinesAreCopiedAndAPointerPastTheLastLineStopsAtIt()
    {
        using var repository = TwoCommits();
        using var harness = await Harness.LoadAsync(repository, new FixedTargets("a.txt", repository.Head));
        await harness.OpenAsync();

        harness.Input.OnDragStarted(2, 3);
        harness.Input.OnDragged(Height, 3);
        harness.Input.TryExecute(ActionIds.VisualCopy, null);

        Assert.Equal("TWO\nthree", Assert.Single(harness.Copies).Text);
    }

    [Fact]
    public async Task GivenDoubleAndTripleClicks_WhenOnTheCode_ThenTheWordAndTheLineAreSelectedAndTheGutterIsIgnored()
    {
        using var repository = TestRepository.Create().Commit("first", new TestFile("a.txt", "foo bar baz\n"));
        using var harness = await Harness.LoadAsync(repository, new FixedTargets("a.txt", repository.Head));
        await harness.OpenAsync();
        var code = BlameFrameBuilder.CodeStart(Width);

        Assert.False(harness.Input.OnTextClicked(1, 3, true));
        Assert.True(harness.Input.OnTextClicked(1, code + 5, true));
        harness.Input.TryExecute(ActionIds.VisualCopy, null);
        Assert.True(harness.Input.OnTextClicked(1, code + 5, false));
        harness.Input.TryExecute(ActionIds.VisualCopy, null);

        Assert.Equal(["bar", "foo bar baz"], harness.Copies.Select(copy => copy.Text));
    }

    [Fact]
    public async Task GivenADragFromTheCode_WhenTheButtonIsReleased_ThenThePointerSelectionIsReported()
    {
        using var repository = TestRepository.Create().Commit("first", new TestFile("a.txt", "ab\tcd\nsecond\n"));
        using var harness = await Harness.LoadAsync(repository, new FixedTargets("a.txt", repository.Head));
        await harness.OpenAsync();
        var reported = 0;
        harness.Input.PointerSelected += () => reported++;
        var code = BlameFrameBuilder.CodeStart(Width);

        harness.Input.OnDragStarted(1, code + 1);
        harness.Input.OnDragged(2, code + 2);
        Assert.Equal(0, reported);
        harness.Input.OnDragReleased();
        harness.Input.OnDragReleased();

        Assert.Equal(1, reported);
        Assert.True(harness.Input.HasSelection);
    }

    [Fact]
    public async Task GivenADoubleClickOnTheCode_WhenItSelectsTheWord_ThenThePointerSelectionIsReported()
    {
        using var repository = TestRepository.Create().Commit("first", new TestFile("a.txt", "foo bar baz\n"));
        using var harness = await Harness.LoadAsync(repository, new FixedTargets("a.txt", repository.Head));
        await harness.OpenAsync();
        var reported = 0;
        harness.Input.PointerSelected += () => reported++;
        var code = BlameFrameBuilder.CodeStart(Width);

        harness.Input.OnTextClicked(1, 3, true);
        harness.Input.OnTextClicked(1, code + 5, true);

        Assert.Equal(1, reported);
    }

    [Theory]
    [InlineData(VisualCopyMode.OnSelect)]
    [InlineData(VisualCopyMode.CtrlC)]
    public async Task GivenASelection_WhenCopiedOnSelectOrWithCtrlC_ThenTheTextIsCopiedAndTheSelectionStays(
        VisualCopyMode mode)
    {
        using var repository = TestRepository.Create().Commit("first", new TestFile("a.txt", "foo bar baz\n"));
        using var harness = await Harness.LoadAsync(repository, new FixedTargets("a.txt", repository.Head));
        await harness.OpenAsync();
        harness.Input.OnTextClicked(1, BlameFrameBuilder.CodeStart(Width) + 5, true);

        harness.Input.CopySelection(mode);

        Assert.Equal("bar", Assert.Single(harness.Copies).Text);
        Assert.True(harness.Input.HasSelection);
    }

    [Fact]
    public async Task GivenABlameSelection_WhenCtrlCIsPressedTwice_ThenTheFirstCopiesAndTheSecondQuits()
    {
        using var repository = TestRepository.Create().Commit("first", new TestFile("a.txt", "foo bar baz\n"));
        using var harness = await Harness.LoadAsync(repository, new FixedTargets("a.txt", repository.Head));
        await harness.OpenAsync();
        harness.Input.OnTextClicked(1, BlameFrameBuilder.CodeStart(Width) + 5, true);
        var quit = new CountingQuit();
        using var source = new RangeSource(0);
        using var list = new ListView();
        list.Source = source;
        var visual = new VisualRangeInput(harness.Host, new FakePaneLists(list));
        var handler = new CtrlCHandler(harness.Host, visual, harness.Input, quit);

        handler.Press();
        Assert.Equal(0, quit.Forced);
        Assert.Equal("bar", Assert.Single(harness.Copies).Text);
        handler.Press();

        Assert.Equal(1, quit.Forced);
    }

    [Fact]
    public async Task GivenABlameSelection_WhenCopyOnSelectIsOn_ThenAMouseSelectionIsCopiedWhenTheDragIsReleased()
    {
        using var repository = TestRepository.Create().Commit("first", new TestFile("a.txt", "ab\tcd\nsecond\n"));
        using var harness = await Harness.LoadAsync(repository, new FixedTargets("a.txt", repository.Head));
        await harness.OpenAsync();
        using var source = new RangeSource(0);
        using var list = new ListView();
        list.Source = source;
        var config = new LiveConfig(
            new ConfigStartup(Path.Combine(Path.GetTempPath(), "gitunia-no-config"), Path.GetTempPath()).Load(),
            new ConsoleCapabilities(false, ColorDepth.None, true),
            Path.GetTempPath());
        _ = new CopyOnSelect(
            harness.Host,
            new VisualRangeInput(harness.Host, new FakePaneLists(list)),
            harness.Input,
            config); // Wires itself to the pointer events.
        var code = BlameFrameBuilder.CodeStart(Width);

        harness.Input.OnDragStarted(1, code + 1);
        harness.Input.OnDragged(2, code + 2);
        Assert.Empty(harness.Copies);
        harness.Input.OnDragReleased();

        Assert.Equal("b\tcd\nsec", Assert.Single(harness.Copies).Text);
        Assert.True(harness.Input.HasSelection);
    }

    [Fact]
    public async Task GivenABlameSelection_WhenCopyOnSelectIsOff_ThenAReleasedDragCopiesNothing()
    {
        using var repository = TestRepository.Create().Commit("first", new TestFile("a.txt", "ab\tcd\nsecond\n"));
        using var harness = await Harness.LoadAsync(repository, new FixedTargets("a.txt", repository.Head));
        await harness.OpenAsync();
        using var source = new RangeSource(0);
        using var list = new ListView();
        list.Source = source;
        var config = new LiveConfig(
            new ConfigStartup(Path.Combine(Path.GetTempPath(), "gitunia-no-config"), Path.GetTempPath()).Load(),
            new ConsoleCapabilities(false, ColorDepth.None, true),
            Path.GetTempPath());
        _ = new CopyOnSelect(
            harness.Host,
            new VisualRangeInput(harness.Host, new FakePaneLists(list)),
            harness.Input,
            config); // Wires itself to the pointer events.
        config.ChangeOptions(config.Options with { CopyOnSelect = false });
        var code = BlameFrameBuilder.CodeStart(Width);

        harness.Input.OnDragStarted(1, code + 1);
        harness.Input.OnDragged(2, code + 2);
        Assert.Empty(harness.Copies);
        harness.Input.OnDragReleased();

        Assert.Empty(harness.Copies);
        Assert.True(harness.Input.HasSelection);
    }

    [Fact]
    public async Task GivenNoSelection_WhenCopyingOnSelect_ThenNothingIsCopied()
    {
        using var repository = TwoCommits();
        using var harness = await Harness.LoadAsync(repository, new FixedTargets("a.txt", repository.Head));
        await harness.OpenAsync();

        harness.Input.CopySelection(VisualCopyMode.OnSelect);

        Assert.Empty(harness.Copies);
    }

    [Fact]
    public async Task GivenYankOnASelection_WhenCopying_ThenTheSelectionEnds()
    {
        using var repository = TestRepository.Create().Commit("first", new TestFile("a.txt", "foo bar baz\n"));
        using var harness = await Harness.LoadAsync(repository, new FixedTargets("a.txt", repository.Head));
        await harness.OpenAsync();
        harness.Input.OnTextClicked(1, BlameFrameBuilder.CodeStart(Width) + 5, true);

        harness.Input.CopySelection(VisualCopyMode.Yank);

        Assert.Equal("bar", Assert.Single(harness.Copies).Text);
        Assert.False(harness.Input.HasSelection);
    }

    [Fact]
    public async Task GivenALineSelectionOfSeveralCommits_WhenOpeningTheLineHistory_ThenACommittedFileUsesItsShownLines()
    {
        using var repository = TwoCommits();
        using var harness = await Harness.LoadAsync(repository, new FixedTargets("a.txt", repository.Head));
        await harness.OpenAsync();

        harness.Input.TryExecute(ActionIds.VisualToggle, null);
        harness.Input.TryExecute(ActionIds.ListBottom, null);
        harness.Input.TryExecute(ActionIds.LineHistoryOpen, null);

        var read = Assert.Single(harness.Effects.OfType<ReadFileHistoryEffect>());
        Assert.Equal(repository.Head, read.Revision);
        Assert.Equal(new LineRange(1, 3), read.Range);
    }

    [Fact]
    public async Task GivenTwoLinesOfTheSameCommit_WhenOpeningTheLineHistoryOfTheWorkingTreeFile_ThenTheWalkStartsAtThatCommit()
    {
        using var repository = TestRepository.Create().Commit("first", new TestFile("a.txt", "one\ntwo\nthree\n"));
        repository.WriteFile("a.txt", "one\ntwo\nthree\nfour\n");
        using var harness = await Harness.LoadAsync(repository, new FixedTargets("a.txt", null));
        await harness.OpenAsync();

        harness.Input.TryExecute(ActionIds.ListDown, null);
        harness.Input.TryExecute(ActionIds.VisualToggle, null);
        harness.Input.TryExecute(ActionIds.ListDown, null);
        harness.Input.TryExecute(ActionIds.LineHistoryOpen, null);

        var read = Assert.Single(harness.Effects.OfType<ReadFileHistoryEffect>());
        Assert.Equal(repository.Head, read.Revision);
        Assert.Equal(new LineRange(2, 3), read.Range);
    }

    [Fact]
    public async Task GivenALineSelectionAcrossCommittedAndUncommittedLines_WhenOpeningTheLineHistory_ThenItRefusesAndKeepsTheView()
    {
        using var repository = TwoCommits();
        repository.WriteFile("a.txt", "one\nTWO\nthree\nextra\n");
        using var harness = await Harness.LoadAsync(repository, new FixedTargets("a.txt", null));
        await harness.OpenAsync();

        harness.Input.TryExecute(ActionIds.ListBottom, null);
        harness.Input.TryExecute(ActionIds.VisualToggle, null);
        harness.Input.TryExecute(ActionIds.ListUp, null);
        harness.Input.TryExecute(ActionIds.LineHistoryOpen, null);

        Assert.True(harness.Input.IsOpen);
        Assert.Empty(harness.Effects.OfType<ReadFileHistoryEffect>());
        Assert.Equal(BlameReducer.MixedLinesNotice, harness.Statuses.Log.Latest!.Text);
    }

    [Fact]
    public async Task GivenAFileLongerThanTheScreen_WhenDraggingAndDoubleClicking_ThenTheLinesDoNotScrollUnderThePointer()
    {
        var text = string.Concat(Enumerable.Range(1, 100).Select(number => $"word{number} tail\n"));
        using var repository = TestRepository.Create().Commit("first", new TestFile("a.txt", text));
        using var harness = await Harness.LoadAsync(repository, new FixedTargets("a.txt", repository.Head));
        await harness.OpenAsync();
        harness.Input.TryExecute(ActionIds.ListBottom, null);
        var code = BlameFrameBuilder.CodeStart(Width);
        var top = BlameFrameBuilder.TopLine(harness.View, Height);

        harness.Input.OnLineClicked(3, code, out _);
        harness.Input.OnDragStarted(3, code + 1);
        harness.Input.OnDragged(4, code + 2);
        harness.Input.OnDragged(6, code + 3);
        Assert.Equal(top, BlameFrameBuilder.TopLine(harness.View, Height));
        harness.Input.TryExecute(ActionIds.VisualCopy, null);

        Assert.Equal($"ord{top + 3} tail\nword{top + 4} tail\nword{top + 5} tail\nword", Assert.Single(harness.Copies).Text);
    }

    [Fact]
    public async Task GivenAFileLongerThanTheScreen_WhenDoubleClickingAfterAClick_ThenTheWordOfThatRowIsSelected()
    {
        var text = string.Concat(Enumerable.Range(1, 100).Select(number => $"word{number} tail\n"));
        using var repository = TestRepository.Create().Commit("first", new TestFile("a.txt", text));
        using var harness = await Harness.LoadAsync(repository, new FixedTargets("a.txt", repository.Head));
        await harness.OpenAsync();
        harness.Input.TryExecute(ActionIds.ListBottom, null);
        var code = BlameFrameBuilder.CodeStart(Width);
        var top = BlameFrameBuilder.TopLine(harness.View, Height);

        harness.Input.OnLineClicked(2, code + 8, out _);
        harness.Input.OnTextClicked(2, code + 8, true);
        Assert.Equal(top, BlameFrameBuilder.TopLine(harness.View, Height));
        harness.Input.TryExecute(ActionIds.VisualCopy, null);

        Assert.Equal($"tail", Assert.Single(harness.Copies).Text);
    }

    [Fact]
    public async Task GivenAPinnedTop_WhenAKeyIsPressed_ThenTheViewCentresTheCursorAgain()
    {
        var text = string.Concat(Enumerable.Range(1, 100).Select(number => $"line{number}\n"));
        using var repository = TestRepository.Create().Commit("first", new TestFile("a.txt", text));
        using var harness = await Harness.LoadAsync(repository, new FixedTargets("a.txt", repository.Head));
        await harness.OpenAsync();
        harness.Input.TryExecute(ActionIds.ListBottom, null);
        harness.Input.OnLineClicked(3, 0, out _);
        Assert.NotNull(harness.View.PinnedTop);

        harness.Input.TryExecute(ActionIds.ListUp, null);

        Assert.Null(harness.View.PinnedTop);
    }

    private static readonly string[] ToggleLabels = ["-w", "-M", "-C"];

    [Fact]
    public async Task GivenTheChipRow_WhenClickingEachChip_ThenItsToggleViewIsNamedAndOffChipsAreNot()
    {
        using var repository = TwoCommits();
        using var harness = await Harness.LoadAsync(repository, new FixedTargets("a.txt", repository.Head));
        await harness.OpenAsync();
        var row = BlameFrameBuilder.VisibleRows(Height) + 5;
        var text = harness.Input.CurrentFrame()!.Lines[row].Text;

        var views = ToggleLabels
            .Select(label => harness.Input.OnLineClicked(row, text.IndexOf(label, StringComparison.Ordinal), out _))
            .ToArray();

        Assert.Equal(
            new[] { DefaultMouseMap.BlameWhitespaceButton, DefaultMouseMap.BlameMovesButton, DefaultMouseMap.BlameCopiesButton },
            views);
        Assert.Equal(ActionIds.BlameDetectCopies, DefaultMouseMap.Create().Resolve(views[2]!, MouseGesture.Click)!.Action);
        Assert.Null(harness.Input.OnLineClicked(row, 70, out var beyondHandled));
        Assert.False(beyondHandled);
    }

    [Fact]
    public async Task GivenNarrowAndTinyScreens_WhenDrawing_ThenTheGutterShrinksAndATinyScreenDrawsNothing()
    {
        using var repository = TwoCommits();
        using var harness = await Harness.LoadAsync(repository, new FixedTargets("a.txt", repository.Head));
        await harness.OpenAsync();

        harness.Resize(40, 12);
        var narrow = harness.Input.CurrentFrame()!;
        harness.Resize(10, 5);
        var tiny = harness.Input.CurrentFrame();

        Assert.All(narrow.Lines, line => Assert.Equal(40, line.Text.Length));
        Assert.DoesNotContain("ago", narrow.Lines[2].Text);
        Assert.Null(tiny);
    }

    [Fact]
    public async Task GivenTheView_WhenAnActionNobodyOwnsIsPressed_ThenTheViewConsumesItAndStaysOpen()
    {
        using var repository = TwoCommits();
        using var harness = await Harness.LoadAsync(repository, new FixedTargets("a.txt", repository.Head));
        await harness.OpenAsync();

        Assert.True(harness.Input.TryExecute(ActionIds.BranchDelete, null));

        Assert.True(harness.Input.IsOpen);
    }

    private static TestRepository TwoCommits() => TestRepository.Create()
        .Commit("first", new TestFile("a.txt", "one\nthree\n"))
        .Commit("second", new TestFile("a.txt", "one\nTWO\nthree\n"));

    private sealed class FixedTargets(string? path, string? revision) : IBlameTargets
    {
        public BlameTarget Resolve(RepositoryState tab) =>
            path is null ? BlameTarget.Refused("nothing selected") : BlameTarget.Of(path, revision);
    }

    private sealed class Harness : IDisposable
    {
        private readonly UiPump _pump = new(TestContext.Current.CancellationToken);
        private readonly Dispatcher _host;
        private readonly BlameEffectHandler _handler;

        private Harness(IBlameTargets targets)
        {
            _host = new Dispatcher(AppState.Empty, _pump);
            Store = new BlameStore();
            _handler = new BlameEffectHandler(TestGit.Services.Blame, Store, _host);
            Input = new BlameInput(_host, Store, targets, GraphGlyphSet.Unicode);
            _host.EffectRaised += Effects.Add;
            _host.EffectRaised += Route;
            Statuses = new StatusReporter(_host);
            Resize(Width, Height);
        }

        public Dispatcher Host => _host;

        public BlameStore Store { get; }

        public BlameInput Input { get; }

        public List<AppEffect> Effects { get; } = [];

        public List<CopyToClipboardEffect> Copies { get; } = [];

        public StatusReporter Statuses { get; }

        public BlameState View => _host.State.Shell.Blame.View!;

        public static async Task<Harness> LoadAsync(TestRepository repository, IBlameTargets targets)
        {
            var harness = new Harness(targets);
            var location = await TestGit.Services.Discovery.DiscoverAsync(
                repository.WorkingTreePath,
                TestContext.Current.CancellationToken);
            harness._host.Dispatch(new OpenRepositoryCommand(location));

            return harness;
        }

        public void Resize(int width, int height) =>
            _host.Dispatch(new ScreenResizedCommand(new ScreenExtent(width, height), _host.Clock.GetUtcNow()));

        public async Task OpenAsync()
        {
            Assert.True(Input.TryExecute(ActionIds.BlameOpen, null));
            await PumpUntilAsync(() => _host.State.Shell.Blame.View is { IsComplete: true });
        }

        public Task PumpUntilAsync(Func<bool> condition) => _pump.RunUntilAsync(condition);

        public void Dispose()
        {
        }

        private void Route(AppEffect effect)
        {
            switch (effect)
            {
                case StartBlameEffect start:
                    _handler.Start(start, TestContext.Current.CancellationToken);
                    break;
                case StopBlameEffect stop:
                    _handler.Stop(stop);
                    break;
                case FindBlameMatchEffect find:
                    _handler.Find(find);
                    break;
                case CopyBlameSelectionEffect copy when _handler.CopyOf(copy) is { } text:
                    Copies.Add(text);
                    break;
                case SetKeyModeEffect mode:
                    _host.Dispatch(new SetKeyModeCommand(mode.Mode));
                    break;
                case PostStatusEffect post:
                    _host.Dispatch(new StatusPostedCommand(post.Text, post.Severity, _host.Clock.GetUtcNow()));
                    break;
            }
        }
    }
}
