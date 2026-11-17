using Gitunia.Core.Blame;
using Gitunia.Core.FileHistory;
using Gitunia.Core.Hints;
using Gitunia.Core.Keymap;
using Gitunia.Core.Overlays;
using Gitunia.Core.Reflog;
using Gitunia.Core.Workspaces.Panes;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Commits.Models;
using Gitunia.Git.History.Models;
using Gitunia.Git.Reflog.Models;
using Gitunia.Tui.Rendering.Graph;
using Gitunia.Tui.Rendering.Overlays;
using Gitunia.Tui.Rendering.Screen;
using Gitunia.Tui.Rendering.Styling;
using Xunit;

namespace Gitunia.Tui.Tests.Rendering.Overlays;

// The contract of HintSet on every surface that draws it in a footer: the footer shows the declared hints, and every
// hint with an action is a click target of the frame that runs the same action as its key.
public class FooterHintClickTests
{
    private const int FooterFromEnd = 2;

    private static readonly DateTimeOffset Now = new(2026, 11, 17, 12, 0, 0, TimeSpan.Zero);

    public static TheoryData<string> Surfaces() =>
    [
        "reflog",
        "file-history",
        "blame",
        "palette",
        "mainline",
        "prompt-text",
        "prompt-filter",
        "prompt-list",
        "prompt-choice",
    ];

    [Theory]
    [MemberData(nameof(Surfaces))]
    public void GivenASurface_WhenItsFooterIsDrawn_ThenItShowsTheDeclaredHints(string surface)
    {
        var drawn = Draw(surface);

        var footer = drawn.Frame.Lines[^FooterFromEnd].Text;
        Assert.NotEmpty(drawn.Footer);
        Assert.All(drawn.Footer, hint => Assert.Contains(Text(hint), footer, StringComparison.Ordinal));
    }

    [Theory]
    [MemberData(nameof(Surfaces))]
    public void GivenASurface_WhenClickingTheKeyOfAFooterHint_ThenTheFrameRunsTheActionOfThatKey(string surface)
    {
        var drawn = Draw(surface);
        var line = drawn.Frame.Lines.Count - FooterFromEnd;
        var footer = drawn.Frame.Lines[line].Text;

        foreach (var hint in drawn.Footer.Where(hint => !hint.Actions.IsDefaultOrEmpty))
        {
            var start = footer.IndexOf(Text(hint), StringComparison.Ordinal);
            var alternatives = hint.Keys == "/" ? ["/"] : hint.Keys.Split('/');
            var column = start;
            for (var index = 0; index < alternatives.Length; index++)
            {
                var expected = hint.Actions.Length == alternatives.Length ? hint.Actions[index] : hint.Actions[0];
                var target = drawn.Frame.TargetAt(line, column);

                Assert.True(target is not null, $"No target for {alternatives[index]} of '{Text(hint)}' on {surface}.");
                Assert.Equal(expected, target.Action);
                column += alternatives[index].Length + 1;
            }

            Assert.Equal(hint.Actions[0], drawn.Frame.TargetAt(line, start + hint.Keys.Length + 1)?.Action);
        }
    }

    [Theory]
    [MemberData(nameof(Surfaces))]
    public void GivenASurface_WhenClickingAHintWithoutAnAction_ThenNothingIsHit(string surface)
    {
        var drawn = Draw(surface);
        var line = drawn.Frame.Lines.Count - FooterFromEnd;
        var footer = drawn.Frame.Lines[line].Text;

        foreach (var note in drawn.Footer.Where(hint => hint.Actions.IsDefaultOrEmpty))
        {
            var start = footer.IndexOf(Text(note), StringComparison.Ordinal);

            Assert.Null(drawn.Frame.TargetAt(line, start));
        }
    }

    [Fact]
    public void GivenTheReflog_WhenReadingTheStatusBarAndTheFooter_ThenTheyComeFromTheSameDeclaration()
    {
        var state = ReflogOf(3);

        var hints = HintSet.ForReflog(state);

        Assert.Equal(HintSet.ForReflogList(), hints);
        Assert.Equal(
            ActionRow.RowsOf(hints).Count + ActionRow.FooterOf(hints).Count,
            hints.Count);
        Assert.All(ActionRow.RowsOf(hints), row => Assert.False(string.IsNullOrEmpty(row.Detail)));
    }

    [Fact]
    public void GivenTheReflogSearchIsBeingTyped_WhenReadingTheHints_ThenTheStatusBarAndTheFooterSayCancel()
    {
        var state = ReflogOf(3).OpenSearch();

        var frame = ReflogFrameBuilder.Build(state, 100, 24, GraphGlyphSet.Unicode, Now)!;

        Assert.Equal([Text(HintSet.ForReflog(state)[0])], HintSet.ForReflog(state).Select(Text));
        Assert.Contains("Esc cancel", frame.Lines[^FooterFromEnd].Text, StringComparison.Ordinal);
        Assert.Equal(ActionIds.Cancel, frame.TargetAt(frame.Lines.Count - FooterFromEnd, FirstKeyColumn(frame))?.Action);
    }

    [Fact]
    public void GivenAnActionRowOfTheReflog_WhenTheKeyIsRebound_ThenTheRowShowsTheNewKey()
    {
        var bindings = DefaultKeymap.CreateBindings()
            .Select(binding => binding.Action == ActionIds.ResetBranch
                ? binding with { Sequence = KeySequenceParser.Parse("Alt-9") }
                : binding);
        var keys = new ChipKeys(DefaultKeymap.Create().WithBindings(bindings));

        var frame = ReflogFrameBuilder.Build(ReflogOf(3), 100, 24, GraphGlyphSet.Unicode, Now, keys)!;

        var rows = string.Join('\n', frame.Lines.Select(line => line.Text));
        Assert.Contains("Alt-9", rows, StringComparison.Ordinal);
        Assert.DoesNotContain("<space>bx", rows, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenAChordGroupOrANote_WhenTheStatusBarIsDrawn_ThenOnlyHintsWithActionsAreClickable()
    {
        var hints = HintSet.For(PaneKind.Diff);
        var model = new ScreenModel(
            "repo",
            null,
            1,
            1,
            "NORMAL",
            string.Empty,
            1,
            1,
            false,
            string.Empty,
            Hints: hints);

        var targets = ChromeBuilder.StatusTargets(model, 200, GraphGlyphSet.Unicode);
        var text = string.Concat(ChromeBuilder
            .Status(model, 200, GraphGlyphSet.Unicode).Segments.Select(segment => segment.Text));

        Assert.Equal(ActionIds.HelpShow, targets.First(target => target.Contains(0, text.IndexOf("? help", StringComparison.Ordinal))).Action);
        Assert.Equal(ActionIds.AppQuit, targets.First(target => target.Contains(0, text.IndexOf("q quit", StringComparison.Ordinal))).Action);
        Assert.DoesNotContain(targets, target => target.Contains(0, text.IndexOf("<space>v", StringComparison.Ordinal)));
        Assert.All(targets, target => Assert.Equal(0, target.Line));
    }

    [Fact]
    public void GivenTheStatusBarIsCutShort_WhenAHintDoesNotFit_ThenItHasNoTargetBeyondTheBar()
    {
        var model = new ScreenModel(
            "repo", null, 1, 1, "NORMAL", string.Empty, 1, 1, false, string.Empty);

        var targets = ChromeBuilder.StatusTargets(model, 60, GraphGlyphSet.Unicode);

        Assert.All(targets, target => Assert.True(target.End <= 60));
    }

    private static int FirstKeyColumn(OverlayFrame frame) =>
        frame.Lines[^FooterFromEnd].Text.IndexOf("Esc", StringComparison.Ordinal);

    private static string Text(KeyHint hint) => hint.Keys.Length == 0 ? hint.Description : $"{hint.Keys} {hint.Description}";

    private static Surface Draw(string surface) => surface switch
    {
        "reflog" => OfFooter(ReflogFrameBuilder.Build(ReflogOf(3), 110, 24, GraphGlyphSet.Unicode, Now)!, HintSet.ForReflogList()),
        "file-history" => OfFooter(
            FileHistoryFrameBuilder.Build(FileHistoryOf(3), 110, 24, GraphGlyphSet.Unicode)!,
            HintSet.ForFileHistory()),
        "blame" => OfFooter(
            BlameFrameBuilder.Build(
                new BlameState(new TabId(1), "a.txt", "a.txt", LineCount: 2),
                null,
                Now,
                110,
                30,
                GraphGlyphSet.Unicode)!,
            HintSet.ForBlameList()),
        "palette" => OfFooter(
            PaletteFrameBuilder.Build(
                new PaletteState(PaletteEntryBuilder.Build(DefaultKeymap.Create(), DefaultKeymap.GraphView)),
                100,
                24,
                GraphGlyphSet.Unicode)!,
            HintSet.ForPalette()),
        "mainline" => OfFooter(
            MainlineFrameBuilder.Build("Revert merge", ["aaaaaaa main", "bbbbbbb topic"], 100, 24, GraphGlyphSet.Unicode, 2)!,
            HintSet.ForMainline()),
        "prompt-text" => OfPrompt(PromptPopup.ForText("New branch", "Name of the branch", "x", "create")),
        "prompt-filter" => OfPrompt(PromptPopup.ForFilter(
            "Switch branch",
            "Pick one",
            "m",
            new PickerWindow(["main", "master"], 0, "1/2"),
            "switch")),
        "prompt-list" => OfPrompt(new PromptPopup(
            "Pick",
            ["Pick one"],
            null,
            ["one", "two"],
            [
                PromptPopup.Accept("pick"),
                new PromptHint("j/k/h/l", "move", [ActionIds.ListDown, ActionIds.ListUp, ActionIds.ListUp, ActionIds.ListDown]),
                new PromptHint("Esc", "cancel", [ActionIds.Cancel]),
            ],
            Highlight: 0)),
        "prompt-choice" => OfPrompt(PromptPopup.ForChoice("Reset", "Mode", ["s  soft", "h  hard"])),
        _ => throw new ArgumentOutOfRangeException(nameof(surface), surface, "Unknown surface."),
    };

    private static Surface OfFooter(OverlayFrame frame, IReadOnlyList<KeyHint> hints) =>
        new(frame, ActionRow.FooterOf(hints));

    private static Surface OfPrompt(PromptPopup popup)
    {
        var frame = PromptPopupFrameBuilder.Build(popup, 100, 30, GraphGlyphSet.Unicode)!;
        var hints = popup.Hints
            .Select(hint => new KeyHint(hint.Keys, hint.Label, hint.Actions.IsDefault ? [] : hint.Actions))
            .ToList();

        return new Surface(frame, hints);
    }

    private static ReflogState ReflogOf(int count) => new(
        new TabId(1),
        "HEAD",
        "HEAD",
        [.. Enumerable.Range(0, count).Select(index =>
            new ReflogEntry(index, new string('a', 40), "commit", $"message {index}", Now.AddHours(-index)))]);

    private static FileHistoryState FileHistoryOf(int count) =>
        new(
            new TabId(1),
            "a.txt",
            "HEAD",
            [.. Enumerable.Range(0, count).Select(index => new FileHistoryEntry(
                new string((char)('a' + index), 40),
                "Ann",
                "2026-11-02",
                $"commit {index}",
                "a.txt",
                null,
                FileChangeKind.Modified))]);

    private sealed record Surface(OverlayFrame Frame, IReadOnlyList<KeyHint> Footer);
}
