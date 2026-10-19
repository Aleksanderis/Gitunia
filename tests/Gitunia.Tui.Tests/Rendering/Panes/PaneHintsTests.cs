using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Workspaces.Panes;
using Gitunia.Tui.Rendering.Graph;
using Gitunia.Tui.Rendering.Panes;
using Gitunia.Tui.Rendering.Screen;
using Xunit;

namespace Gitunia.Tui.Tests.Rendering.Panes;

public class PaneHintsTests
{
    public static TheoryData<PaneKind> Panes() => [.. Enum.GetValues<PaneKind>()];

    [Theory]
    [InlineData(PaneKind.Graph, "gg/G")]
    [InlineData(PaneKind.Sidebar, "/")]
    [InlineData(PaneKind.Detail, "l")]
    [InlineData(PaneKind.Diff, "<space>vw")]
    [InlineData(PaneKind.WorkingUnstaged, "y")]
    [InlineData(PaneKind.WorkingStaged, "y")]
    [InlineData(PaneKind.WorkingUnstagedDiff, "]c/[c")]
    [InlineData(PaneKind.WorkingStagedDiff, "<space>vw")]
    public void GivenAPane_WhenAskingForHints_ThenTheyMentionThatPanesKeys(PaneKind pane, string expectedKeys)
    {
        Assert.Contains(PaneHints.For(pane), hint => hint.Keys == expectedKeys);
    }

    [Theory]
    [InlineData(PaneKind.Graph)]
    [InlineData(PaneKind.Diff)]
    [InlineData(PaneKind.Detail)]
    [InlineData(PaneKind.WorkingUnstaged)]
    [InlineData(PaneKind.WorkingStaged)]
    [InlineData(PaneKind.WorkingUnstagedDiff)]
    [InlineData(PaneKind.WorkingStagedDiff)]
    public void GivenAPaneWithTheQuickJump_WhenAskingForHints_ThenTheyOfferIt(PaneKind pane)
    {
        Assert.Contains(PaneHints.For(pane), hint => hint.Keys == "<space>j" && hint.Actions.Contains(ActionIds.QuickJump));
    }

    [Theory]
    [InlineData(PaneKind.Graph)]
    [InlineData(PaneKind.Sidebar)]
    [InlineData(PaneKind.Detail)]
    public void GivenAHistoryPane_WhenAskingForHints_ThenTheyShowHowToOpenTheWorkingTree(PaneKind pane)
    {
        Assert.Contains(PaneHints.For(pane), hint => hint.Keys == "<space>s" && hint.Description == "working tree");
    }

    [Theory]
    [MemberData(nameof(Panes))]
    public void GivenAPane_WhenAskingForHints_ThenEveryHintHasKeysAndADescription(PaneKind pane)
    {
        Assert.NotEmpty(PaneHints.For(pane));

        foreach (var hint in PaneHints.For(pane))
        {
            Assert.False(string.IsNullOrWhiteSpace(hint.Keys), "A hint has no keys.");
            Assert.False(string.IsNullOrWhiteSpace(hint.Description), $"The hint {hint.Keys} has no description.");
        }
    }

    [Fact]
    public void GivenThePickerHints_WhenListing_ThenEveryHintHasADescription()
    {
        foreach (var hint in PaneHints.ForPicker())
        {
            Assert.False(string.IsNullOrWhiteSpace(hint.Description), $"The hint {hint.Keys} has no description.");
        }
    }

    [Theory]
    [MemberData(nameof(Panes))]
    public void GivenAPane_WhenAskingForHints_ThenNoKeyUsesTheCaretNotation(PaneKind pane)
    {
        Assert.All(PaneHints.For(pane), hint => Assert.DoesNotContain('^', hint.Keys));
    }

    [Fact]
    public void GivenAFocusedDiffPane_WhenBuildingTheStatusBar_ThenItShowsKeysNextToTheirDescriptions()
    {
        var model = new ScreenModel("repo", null, 1, 1, "NORMAL", string.Empty, 1, 1, false, string.Empty, Hints: PaneHints.For(PaneKind.Diff));

        var text = StatusText(model);

        Assert.Contains("]c/[c hunk", text, StringComparison.Ordinal);
        Assert.Contains("w wrap", text, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenNoHints_WhenBuildingTheStatusBar_ThenItShowsTheGraphHintsWithTheSpelledOutNotation()
    {
        var model = new ScreenModel("repo", null, 1, 1, "NORMAL", string.Empty, 1, 1, false, string.Empty);

        var text = StatusText(model);

        Assert.Contains("Ctrl-d/u page", text, StringComparison.Ordinal);
        Assert.DoesNotContain('^', text);
    }

    [Theory]
    [MemberData(nameof(Panes))]
    public void GivenAPaneAt80Columns_WhenBuildingTheStatusBar_ThenHelpAndCommandsStayVisible(PaneKind pane)
    {
        var model = new ScreenModel("repo", null, 1, 1, "NORMAL", string.Empty, 1, 1, false, "Could not read the stashes: boom", Hints: PaneHints.For(pane));

        var text = string.Concat(ChromeBuilder.Status(model, 80, GraphGlyphSet.Unicode).Segments.Select(segment => segment.Text));

        Assert.Contains("? help  : commands", text, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(Panes))]
    public void GivenAPane_WhenCheckingItsHints_ThenEveryKeyTriggersItsActionInTheDefaultKeymapForThatPane(PaneKind pane)
    {
        var view = ViewFor(pane);

        AssertKeysTriggerActions(PaneHints.For(pane), KeyMode.Normal, view);
    }

    [Fact]
    public void GivenTheMessageEditingHints_WhenCheckingThem_ThenEveryKeyTriggersItsActionInTheMessageViewInInsertMode()
    {
        AssertKeysTriggerActions(PaneHints.ForMessageEditing(), KeyMode.Insert, DefaultKeymap.WorkingMessageView);
    }

    [Fact]
    public void GivenTheConfirmHints_WhenCheckingThem_ThenEveryKeyTriggersItsActionInConfirmMode()
    {
        AssertKeysTriggerActions(PaneHints.ForConfirm(), KeyMode.Confirm, DefaultKeymap.ConfirmView);
    }

    [Fact]
    public void GivenTheReflogHints_WhenCheckingThem_ThenEveryKeyTriggersItsActionInTheReflogView()
    {
        AssertKeysTriggerActions(PaneHints.ForReflog(), KeyMode.Normal, DefaultKeymap.ReflogView);
    }

    [Fact]
    public void GivenTheRebaseEditorHints_WhenCheckingThem_ThenEveryKeyTriggersItsActionInTheEditorView()
    {
        AssertKeysTriggerActions(PaneHints.ForRebaseEditor(), KeyMode.Normal, DefaultKeymap.RebaseEditorView);
    }

    [Fact]
    public void GivenTheRebaseMessageHints_WhenCheckingThem_ThenEveryKeyTriggersItsActionInInsertMode()
    {
        AssertKeysTriggerActions(PaneHints.ForRebaseMessage(), KeyMode.Insert, DefaultKeymap.RebaseEditorView);
    }

    [Fact]
    public void GivenTheSettingsHints_WhenCheckingThem_ThenEveryKeyTriggersItsActionInTheSettingsView()
    {
        AssertKeysTriggerActions(PaneHints.ForSettings(), KeyMode.Normal, DefaultKeymap.SettingsView);
    }

    [Fact]
    public void GivenThePickerHints_WhenCheckingThem_ThenEveryKeyTriggersItsActionInThePickerView()
    {
        AssertKeysTriggerActions(PaneHints.ForPicker(), KeyMode.Insert, DefaultKeymap.PickerView);
    }

    [Fact]
    public void GivenThePaletteHints_WhenCheckingThem_ThenEveryKeyTriggersItsActionInThePaletteView()
    {
        AssertKeysTriggerActions(PaneHints.ForPalette(), KeyMode.Command, DefaultKeymap.PaletteView);
    }

    private static string ViewFor(PaneKind pane) => pane switch
    {
        PaneKind.Sidebar => DefaultKeymap.SidebarView,
        PaneKind.Detail => DefaultKeymap.DetailView,
        PaneKind.Diff => DefaultKeymap.DiffView,
        PaneKind.WorkingUnstaged => DefaultKeymap.WorkingUnstagedView,
        PaneKind.WorkingStaged => DefaultKeymap.WorkingStagedView,
        PaneKind.WorkingUnstagedDiff => DefaultKeymap.WorkingUnstagedDiffView,
        PaneKind.WorkingStagedDiff => DefaultKeymap.WorkingStagedDiffView,
        PaneKind.WorkingMessage => DefaultKeymap.WorkingMessageView,
        _ => DefaultKeymap.GraphView,
    };

    private static void AssertKeysTriggerActions(IReadOnlyList<KeyHint> hints, KeyMode mode, string view)
    {
        var bindings = DefaultKeymap.Create().EffectiveBindings(mode, view);
        foreach (var hint in hints)
        {
            var alternatives = ExpandAlternatives(hint.Keys).ToList();
            Assert.True(alternatives.Count == hint.Actions.Length, $"The hint '{hint.Keys} {hint.Description}' lists {alternatives.Count} keys but names {hint.Actions.Length} actions.");
            for (var index = 0; index < alternatives.Count; index++)
            {
                var sequence = KeySequenceParser.Parse(alternatives[index]);
                var binding = bindings.FirstOrDefault(candidate => candidate.Sequence.Equals(sequence));
                Assert.NotNull(binding);
                Assert.True(binding.Action == hint.Actions[index], $"The hint '{hint.Keys} {hint.Description}' shows '{alternatives[index]}' for {hint.Actions[index]}, but the {view} view binds it to {binding.Action}.");
            }
        }
    }

    // A hint such as "Ctrl-d/u" or "gg/G" lists alternatives; a bare character after "Ctrl-" reuses it (other modifiers are not supported yet). Each alternative maps to the action at the same index.
    private static IEnumerable<string> ExpandAlternatives(string keys)
    {
        const string ctrlPrefix = "Ctrl-";
        const string leaderPrefix = "<space>";
        const string slashKey = "/";
        var alternatives = keys == slashKey ? [keys] : keys.Split(slashKey[0]);
        for (var index = 0; index < alternatives.Length; index++)
        {
            var alternative = alternatives[index];
            if (index > 0 && alternative.Length == 1 && alternatives[0].StartsWith(ctrlPrefix, StringComparison.Ordinal))
            {
                yield return $"{ctrlPrefix}{alternative}";
            }
            else if (index > 0 && alternative.Length == 1
                && alternatives[0].StartsWith(leaderPrefix, StringComparison.Ordinal))
            {
                yield return $"{leaderPrefix}{alternative}";
            }
            else
            {
                yield return alternative;
            }
        }
    }

    [Fact]
    public void GivenACutLog_WhenDrawingTheStatusBar_ThenThePositionShowsLoadedAndTotalWithAPlus()
    {
        var model = new ScreenModel(
            "repo", null, 1, 1, "NORMAL", string.Empty, 12, 100000, false, string.Empty, TruncatedTotal: 1204331);

        Assert.Contains("12/100000+ of 1204331", StatusText(model), StringComparison.Ordinal);
    }

    [Fact]
    public void GivenAWholeLog_WhenDrawingTheStatusBar_ThenThePositionHasNoTotal()
    {
        var model = new ScreenModel("repo", null, 1, 1, "NORMAL", string.Empty, 12, 100, false, string.Empty);

        var text = StatusText(model);

        Assert.Contains("12/100 ", text, StringComparison.Ordinal);
        Assert.DoesNotContain(" of ", text, StringComparison.Ordinal);
    }

    private static string StatusText(ScreenModel model) =>
        string.Concat(ChromeBuilder.Status(model, 160, GraphGlyphSet.Unicode).Segments.Select(segment => segment.Text));
}
