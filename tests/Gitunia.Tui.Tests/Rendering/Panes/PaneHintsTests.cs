using Gitunia.Core.Config.Models;
using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Keymap.Repeat;
using Gitunia.Core.Workspaces.Panes;
using Gitunia.Tui.Rendering.Graph;
using Gitunia.Tui.Rendering.Panes;
using Gitunia.Tui.Rendering.Screen;
using Xunit;
using Xunit.Sdk;

namespace Gitunia.Tui.Tests.Rendering.Panes;

public class PaneHintsTests
{
    public static TheoryData<PaneKind> Panes() => [.. Enum.GetValues<PaneKind>()];

    // One fact walks every pane (or hint set) and names the ones that fail, instead of one test row each.
    private static void ForEvery<T>(IEnumerable<T> items, Action<T> check)
    {
        var failures = new List<string>();
        foreach (var item in items)
        {
            try
            {
                check(item);
            }
            catch (XunitException exception)
            {
                failures.Add($"{item}: {exception.Message}");
            }
        }

        Assert.Empty(failures);
    }

    [Fact]
    public void GivenMinimalMode_WhenLimitingAPanesHints_ThenOnlyHelpCommandsAndQuitRemain()
        => ForEvery(Enum.GetValues<PaneKind>(), CheckOnlyHelpCommandsAndQuitRemain);

    private static void CheckOnlyHelpCommandsAndQuitRemain(PaneKind pane)
    {
        var limited = PaneHints.Limit(PaneHints.For(pane), StatusBarHintMode.Minimal);

        Assert.Equal(["?", ":", "q"], limited.Select(hint => hint.Keys));
        Assert.All(limited, hint => Assert.Contains(PaneHints.For(pane), other => other == hint));
    }

    [Fact]
    public void GivenFullMode_WhenLimitingAtEachLevel_ThenOnlyHintsWithAListedActionRemainAndEveryoneKeepsThePinnedOnes()
        => ForEvery(Enum.GetValues<PaneKind>(), CheckOnlyHintsWithAListedActionRemainAndEveryoneKeepsThePinnedOnes);

    private static void CheckOnlyHintsWithAListedActionRemainAndEveryoneKeepsThePinnedOnes(PaneKind pane)
    {
        var hints = PaneHints.For(pane);

        foreach (var level in Enum.GetValues<CommandLevel>())
        {
            var limited = PaneHints.Limit(hints, StatusBarHintMode.Full, level);

            Assert.Equal(["?", ":", "q"], limited.Take(3).Select(hint => hint.Keys));
            Assert.All(limited, hint => Assert.True(PaneHints.IsListedAt(hint, level)));
            Assert.Equal(hints.Count(hint => PaneHints.IsListedAt(hint, level)), limited.Count);
        }
    }

    [Fact]
    public void GivenTheMessageBoxHints_WhenLimitingByLevel_ThenTheEditorHintAppearsOnlyAtItsLevel()
    {
        var hints = PaneHints.For(PaneKind.WorkingMessage);
        var editorLevel = ActionCatalog.LevelOf(ActionIds.ToolCommitEditor)!.Value;

        Assert.NotEqual(CommandLevel.Everyday, editorLevel);
        Assert.DoesNotContain(
            PaneHints.Limit(hints, StatusBarHintMode.Full, CommandLevel.Everyday),
            hint => hint.Actions.Contains(ActionIds.ToolCommitEditor));
        Assert.Contains(
            PaneHints.Limit(hints, StatusBarHintMode.Full, editorLevel),
            hint => hint.Actions.Contains(ActionIds.ToolCommitEditor));
    }

    [Fact]
    public void GivenAnAboveLevelHint_WhenLimitingToMinimalOrOff_ThenTheModeStillWins()
    {
        var hints = PaneHints.For(PaneKind.WorkingMessage);

        Assert.Equal(3, PaneHints.Limit(hints, StatusBarHintMode.Minimal).Count);
        Assert.Empty(PaneHints.Limit(hints, StatusBarHintMode.Off));
    }

    [Fact]
    public void GivenOffMode_WhenLimitingHints_ThenNoneRemain()
    {
        Assert.Empty(PaneHints.Limit(PaneHints.For(PaneKind.Graph), StatusBarHintMode.Off));
    }

    [Fact]
    public void GivenFullMode_WhenLimitingHints_ThenTheyAreUnchanged()
    {
        var hints = PaneHints.For(PaneKind.Graph);

        Assert.Same(hints, PaneHints.Limit(hints, StatusBarHintMode.Full));
    }

    [Fact]
    public void GivenAPane_WhenAskingForHints_ThenTheyMentionThatPanesKeys()
    {
        var failures = new List<string>();
        (PaneKind pane, string expectedKeys)[] rows =
        [
            (PaneKind.Graph, "gg/G"),
            (PaneKind.Sidebar, "/"),
            (PaneKind.Detail, "Enter"),
            (PaneKind.Diff, "<space>v"),
            (PaneKind.WorkingUnstaged, "<space>c"),
            (PaneKind.WorkingStaged, "<space>c"),
            (PaneKind.WorkingUnstagedDiff, "]c/[c"),
            (PaneKind.WorkingStagedDiff, "<space>v"),
        ];

        foreach (var (pane, expectedKeys) in rows)
        {
            try
            {
                CheckTheyMentionThatPanesKeys(pane, expectedKeys);
            }
            catch (XunitException exception)
            {
                failures.Add($"{pane}, {expectedKeys}: {exception.Message}");
            }
        }

        Assert.Empty(failures);
    }

    private static void CheckTheyMentionThatPanesKeys(PaneKind pane, string expectedKeys)
    {
        Assert.Contains(PaneHints.For(pane), hint => hint.Keys == expectedKeys);
    }

    [Fact]
    public void GivenAPaneWithTheQuickJump_WhenAskingForHints_ThenTheyOfferIt()
    {
        var failures = new List<string>();
        PaneKind[] rows =
        [
            PaneKind.Graph,
            PaneKind.Diff,
            PaneKind.Detail,
            PaneKind.WorkingUnstaged,
            PaneKind.WorkingStaged,
            PaneKind.WorkingUnstagedDiff,
            PaneKind.WorkingStagedDiff,
        ];

        foreach (var pane in rows)
        {
            try
            {
                CheckTheyOfferIt(pane);
            }
            catch (XunitException exception)
            {
                failures.Add($"{pane}: {exception.Message}");
            }
        }

        Assert.Empty(failures);
    }

    private static void CheckTheyOfferIt(PaneKind pane)
    {
        Assert.Contains(PaneHints.For(pane), hint => hint.Keys == "<space>j" && hint.Actions.Contains(ActionIds.QuickJump));
    }

    [Theory]
    [InlineData(PaneKind.Graph)]
    [InlineData(PaneKind.Sidebar)]
    [InlineData(PaneKind.Detail)]
    public void GivenAHistoryPane_WhenAskingForHints_ThenTheyShowHowToOpenTheWorkingTree(PaneKind pane)
    {
        Assert.Contains(PaneHints.For(pane), hint => hint.Keys == "<space>s" && hint.Description == "Changes view");
    }

    [Fact]
    public void GivenAPane_WhenAskingForHints_ThenEveryHintHasKeysAndADescription()
        => ForEvery(Enum.GetValues<PaneKind>(), CheckEveryHintHasKeysAndADescription);

    private static void CheckEveryHintHasKeysAndADescription(PaneKind pane)
    {
        Assert.NotEmpty(PaneHints.For(pane));

        foreach (var hint in PaneHints.For(pane))
        {
            Assert.False(string.IsNullOrWhiteSpace(hint.Keys), "A hint has no keys.");
            Assert.False(string.IsNullOrWhiteSpace(hint.Description), $"The hint {hint.Keys} has no description.");
        }
    }

    [Fact]
    public void GivenAMarkedCommit_WhenAskingForGraphHints_ThenTheyNameMovingTheMarkAndEndingTheCompare()
    {
        var hints = PaneHints.ForGraphComparing();

        Assert.Contains(hints, hint => hint.Keys == "<space>=" && hint.Description == "move mark");
        Assert.Contains(hints, hint => hint.Keys == "Esc" && hint.Description == "end compare");
        Assert.Contains(hints, hint => hint.Keys == "gg/G");
        AssertKeysTriggerActions(hints, KeyMode.Normal, DefaultKeymap.GraphView);
    }

    [Fact]
    public void GivenAFilesContentInTheDiffPane_WhenHintsAreAsked_ThenNoHunkFileOrDiffOptionHintsShow()
    {
        var hints = PaneHints.For(PaneKind.Diff, showsFileContent: true);

        string[] diffOnly = ["hunk", "file", "context", "whole file", "side by side", "ignore space"];
        Assert.DoesNotContain(hints, hint => diffOnly.Contains(hint.Description));
        Assert.Contains(hints, hint => hint.Description == "copy file");
        Assert.Equal(PaneHints.For(PaneKind.Diff), PaneHints.For(PaneKind.Diff, showsFileContent: false));
    }

    [Fact]
    public void GivenAConflictedFileInTheUnstagedList_WhenAskingForHints_ThenTheEditorLeadsTheListsOwnHints()
    {
        var hints = PaneHints.For(PaneKind.WorkingUnstaged, showsFileContent: false, onConflict: true);

        Assert.Equal("Enter", hints[3].Keys);
        Assert.Equal("conflict editor", hints[3].Description);
        Assert.DoesNotContain(hints, hint => hint.Description == "diff");
        Assert.DoesNotContain(PaneHints.For(PaneKind.WorkingUnstaged), hint => hint.Description == "conflict editor");
        AssertKeysTriggerActions(hints, KeyMode.Normal, DefaultKeymap.WorkingUnstagedView);
    }

    [Fact]
    public void GivenAConflictedFileInItsDiff_WhenAskingForHints_ThenTheLeaderKeysOfTheEditorLeadAndAreBound()
    {
        var hints = PaneHints.For(PaneKind.WorkingUnstagedDiff, showsFileContent: false, onConflict: true);

        Assert.Equal("<space>gr", hints[3].Keys);
        Assert.Equal("conflict editor", hints[3].Description);
        AssertKeysTriggerActions(hints, KeyMode.Normal, DefaultKeymap.WorkingUnstagedDiffView);
    }

    [Theory]
    [InlineData(PaneKind.Graph)]
    [InlineData(PaneKind.Sidebar)]
    [InlineData(PaneKind.Diff)]
    [InlineData(PaneKind.WorkingStaged)]
    public void GivenAConflictPending_WhenAddingTheEditorHint_ThenItFollowsHelpCommandsAndQuitAndIsBound(PaneKind pane)
    {
        var hints = PaneHints.WithConflictEditor(PaneHints.For(pane));

        Assert.Equal("<space>gr", hints[3].Keys);
        Assert.Equal("conflict editor", hints[3].Description);
        Assert.Equal(PaneHints.For(pane).Count + 1, hints.Count);
        AssertKeysTriggerActions(hints, KeyMode.Normal, ViewFor(pane));
    }

    [Fact]
    public void GivenARefusedConflictedFileInTheUnstagedList_WhenAskingForHints_ThenTheMergetoolLeadsAndTheEditorIsNotOffered()
    {
        var hints = PaneHints.For(PaneKind.WorkingUnstaged, showsFileContent: false, onRefusedConflict: true);

        Assert.Equal("<space>em", hints[3].Keys);
        Assert.Equal("mergetool", hints[3].Description);
        Assert.DoesNotContain(hints, hint => hint.Description is "conflict editor" or "diff");
        AssertKeysTriggerActions(hints, KeyMode.Normal, DefaultKeymap.WorkingUnstagedView);
    }

    [Fact]
    public void GivenARefusedConflictedFileInItsDiff_WhenAskingForHints_ThenTheMergetoolLeads()
    {
        var hints = PaneHints.For(PaneKind.WorkingUnstagedDiff, showsFileContent: false, onRefusedConflict: true);

        Assert.Equal("<space>em", hints[3].Keys);
        Assert.DoesNotContain(hints, hint => hint.Description == "conflict editor");
        AssertKeysTriggerActions(hints, KeyMode.Normal, DefaultKeymap.WorkingUnstagedDiffView);
    }

    [Theory]
    [InlineData(PaneKind.Graph)]
    [InlineData(PaneKind.Sidebar)]
    [InlineData(PaneKind.WorkingStaged)]
    public void GivenNoConflictedFileTheEditorTakes_WhenAddingTheMergetoolHint_ThenItFollowsHelpCommandsAndQuit(
        PaneKind pane)
    {
        var hints = PaneHints.WithConflictMergetool(PaneHints.For(pane));

        Assert.Equal("<space>em", hints[3].Keys);
        Assert.Equal("mergetool", hints[3].Description);
        Assert.Equal(PaneHints.For(pane).Count + 1, hints.Count);
        AssertKeysTriggerActions(hints, KeyMode.Normal, ViewFor(pane));
    }

    [Fact]
    public void GivenHintsThatAlreadyLeadWithTheEditor_WhenAddingTheEditorHint_ThenTheyAreLeftAlone()
    {
        var hints = PaneHints.For(PaneKind.WorkingUnstaged, showsFileContent: false, onConflict: true);

        Assert.Same(hints, PaneHints.WithConflictEditor(hints));
    }

    [Theory]
    [InlineData(PaneKind.Graph)]
    [InlineData(PaneKind.WorkingStaged)]
    public void GivenAConflictedFileButAnotherPane_WhenAskingForHints_ThenTheyAreTheUsualOnes(PaneKind pane)
    {
        Assert.Equal(PaneHints.For(pane), PaneHints.For(pane, showsFileContent: false, onConflict: true));
    }

    [Fact]
    public void GivenThePickerHints_WhenListing_ThenEveryHintHasADescription()
    {
        foreach (var hint in PaneHints.ForPicker())
        {
            Assert.False(string.IsNullOrWhiteSpace(hint.Description), $"The hint {hint.Keys} has no description.");
        }
    }

    [Fact]
    public void GivenAPane_WhenAskingForHints_ThenNoKeyUsesTheCaretNotation()
        => ForEvery(Enum.GetValues<PaneKind>(), CheckNoKeyUsesTheCaretNotation);

    private static void CheckNoKeyUsesTheCaretNotation(PaneKind pane)
    {
        Assert.All(PaneHints.For(pane), hint => Assert.DoesNotContain('^', hint.Keys));
    }

    [Fact]
    public void GivenAFocusedDiffPane_WhenBuildingTheStatusBar_ThenItShowsKeysNextToTheirDescriptions()
    {
        var model = new ScreenModel("repo", null, 1, 1, "NORMAL", string.Empty, 1, 1, false, string.Empty, Hints: PaneHints.For(PaneKind.Diff));

        var text = StatusText(model);

        Assert.Contains("]c/[c hunk", text, StringComparison.Ordinal);
        Assert.Contains("view options", text, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(PaneKind.Diff)]
    [InlineData(PaneKind.WorkingUnstagedDiff)]
    [InlineData(PaneKind.WorkingStagedDiff)]
    public void GivenADiffPane_WhenAskingForHints_ThenTheViewOptionsAreOneGroupHintAndTheirKeysStayBound(PaneKind pane)
    {
        var hints = PaneHints.For(pane);

        Assert.Single(hints, hint => hint.Keys == "<space>v");
        Assert.DoesNotContain(
            hints,
            hint => hint.Description is "wrap" or "context" or "whole file" or "ignore space" or "side by side"
                or "load all");
        var bindings = DefaultKeymap.Create().EffectiveBindings(KeyMode.Normal, ViewFor(pane));
        ActionId[] options =
        [
            ActionIds.DiffToggleWrap,
            ActionIds.DiffToggleWholeFile,
            ActionIds.DiffToggleIgnoreWhitespace,
            ActionIds.DiffToggleSideBySide,
            ActionIds.DiffLoadAnyway,
            ActionIds.DiffMoreContext,
            ActionIds.DiffLessContext,
        ];
        Assert.All(options, option => Assert.Contains(bindings, binding => binding.Action == option));
    }

    [Fact]
    public void GivenNoHints_WhenBuildingTheStatusBar_ThenItShowsTheGraphHintsWithTheSpelledOutNotation()
    {
        var model = new ScreenModel("repo", null, 1, 1, "NORMAL", string.Empty, 1, 1, false, string.Empty);

        var text = StatusText(model);

        Assert.Contains("Ctrl-d/u page", text, StringComparison.Ordinal);
        Assert.DoesNotContain('^', text);
    }

    [Fact]
    public void GivenAPaneAt80Columns_WhenBuildingTheStatusBar_ThenHelpAndCommandsStayVisible()
        => ForEvery(Enum.GetValues<PaneKind>(), CheckHelpAndCommandsStayVisible);

    private static void CheckHelpAndCommandsStayVisible(PaneKind pane)
    {
        var model = new ScreenModel("repo", null, 1, 1, "NORMAL", string.Empty, 1, 1, false, "Could not read the stashes: boom", Hints: PaneHints.For(pane));

        var text = string.Concat(ChromeBuilder.Status(model, 80, GraphGlyphSet.Unicode).Segments.Select(segment => segment.Text));

        Assert.Contains("? help  : commands", text, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenAPane_WhenCheckingItsHints_ThenEveryKeyTriggersItsActionInTheDefaultKeymapForThatPane()
        => ForEvery(Enum.GetValues<PaneKind>(), CheckEveryKeyTriggersItsActionInTheDefaultKeymapForThatPane);

    private static void CheckEveryKeyTriggersItsActionInTheDefaultKeymapForThatPane(PaneKind pane)
    {
        var view = ViewFor(pane);

        AssertKeysTriggerActions(PaneHints.For(pane), KeyMode.Normal, view);
    }

    [Fact]
    public void GivenTheDetailChangesHints_WhenCheckingThem_ThenTheViewOptionsAreOnlyThereAndTheirKeysAreBound()
    {
        Assert.DoesNotContain(
            PaneHints.For(PaneKind.Detail),
            hint => hint.Keys.StartsWith("<space>v", StringComparison.Ordinal));
        Assert.Contains(
            PaneHints.ForDetailChanges(),
            hint => hint.Keys == "<space>v" && hint.Description == "view options");
        AssertKeysTriggerActions(PaneHints.ForDetailChanges(), KeyMode.Normal, DefaultKeymap.DetailChangesView);
    }

    [Theory]
    [InlineData(RepeatKind.Context, RepeatMode.ContextView)]
    [InlineData(RepeatKind.Resize, RepeatMode.ResizeView)]
    public void GivenARepeatMode_WhenCheckingItsHints_ThenEveryKeyTriggersItsActionInTheModesView(
        RepeatKind kind,
        string view)
    {
        AssertKeysTriggerActions(PaneHints.ForRepeat(kind), KeyMode.Normal, view);
    }

    public static TheoryData<string> ModalHintSets() =>
        [
            "MessageEditing",
            "Confirm",
            "Reflog",
            "RebaseEditor",
            "ConflictVersions",
            "ConflictVisual",
            "ConflictResult",
            "ConflictEdit",
            "RebaseMessage",
            "Settings",
            "Picker",
            "Palette",
        ];

    [Fact]
    public void GivenAModalHintSet_WhenCheckingItsHints_ThenEveryKeyTriggersItsActionInItsView()
        => ForEvery(ModalHintSets().Select(row => row.Data), CheckEveryKeyTriggersItsActionInItsView);

    private static void CheckEveryKeyTriggersItsActionInItsView(string set)
    {
        switch (set)
        {
            case "MessageEditing":
                AssertKeysTriggerActions(
                    PaneHints.ForMessageEditing(), KeyMode.Insert, DefaultKeymap.WorkingMessageView);
                break;
            case "Confirm":
                AssertKeysTriggerActions(PaneHints.ForConfirm(), KeyMode.Confirm, DefaultKeymap.ConfirmView);
                break;
            case "Reflog":
                AssertKeysTriggerActions(PaneHints.ForReflog(), KeyMode.Normal, DefaultKeymap.ReflogView);
                break;
            case "ConflictVersions":
                AssertKeysTriggerActions(
                    PaneHints.ForConflictVersions(),
                    KeyMode.Normal,
                    DefaultKeymap.ConflictSideView);
                break;
            case "ConflictVisual":
                AssertKeysTriggerActions(
                    PaneHints.ForConflictVisual(),
                    KeyMode.Visual,
                    DefaultKeymap.ConflictSideView);
                break;
            case "ConflictResult":
                AssertKeysTriggerActions(
                    PaneHints.ForConflictResult(),
                    KeyMode.Normal,
                    DefaultKeymap.ConflictResultView);
                break;
            case "ConflictEdit":
                AssertKeysTriggerActions(
                    PaneHints.ForConflictEdit(),
                    KeyMode.Insert,
                    DefaultKeymap.ConflictResultView);
                break;
            case "RebaseEditor":
                AssertKeysTriggerActions(PaneHints.ForRebaseEditor(), KeyMode.Normal, DefaultKeymap.RebaseEditorView);
                break;
            case "RebaseMessage":
                AssertKeysTriggerActions(PaneHints.ForRebaseMessage(), KeyMode.Insert, DefaultKeymap.RebaseEditorView);
                break;
            case "Settings":
                AssertKeysTriggerActions(PaneHints.ForSettings(), KeyMode.Normal, DefaultKeymap.SettingsView);
                break;
            case "Picker":
                AssertKeysTriggerActions(PaneHints.ForPicker(), KeyMode.Insert, DefaultKeymap.PickerView);
                break;
            case "Palette":
                AssertKeysTriggerActions(PaneHints.ForPalette(), KeyMode.Command, DefaultKeymap.PaletteView);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(set), set, "Unknown hint set.");
        }
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
            if (hint.Actions.IsEmpty)
            {
                var prefix = KeySequenceParser.Parse(hint.Keys);
                Assert.True(
                    bindings.Any(
                        candidate => candidate.Sequence.Length > prefix.Length
                            && candidate.Sequence.StartsWith(prefix)),
                    $"The group hint '{hint.Keys} {hint.Description}' has no binding below it in the {view} view.");
                continue;
            }

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

    [Fact]
    public void GivenASelectedSidebarName_WhenDrawingTheStatusBar_ThenItFollowsThePosition()
    {
        var model = new ScreenModel("repo", null, 1, 1, "NORMAL", string.Empty, 3, 9, false, string.Empty)
        {
            SelectedName = "feature/ui/button",
        };

        var text = StatusText(model);

        Assert.Contains("3/9 feature/ui/button ", text, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenALongSelectedName_WhenDrawingANarrowStatusBar_ThenItIsCutInTheMiddleAndTheMessageSurvives()
    {
        var model = new ScreenModel("repo", null, 1, 1, "NORMAL", string.Empty, 3, 9, false, "Checked out main")
        {
            SelectedName = "/home/user/Projects/Gitunia/.claude/worktrees/agent-a58b7c7253cb51622",
        };

        var text = string.Concat(ChromeBuilder.Status(model, 80, GraphGlyphSet.Unicode).Segments.Select(segment => segment.Text));

        Assert.Contains("Checked out main", text, StringComparison.Ordinal);
        Assert.Contains("cb51622", text, StringComparison.Ordinal);
        Assert.Contains("…", text, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenASelectedNameAndAFilterLine_WhenDrawingTheStatusBar_ThenTheNameIsLeftOut()
    {
        var model = new ScreenModel("repo", null, 1, 1, "INSERT", string.Empty, 3, 9, false, string.Empty)
        {
            SelectedName = "feature/x",
            SearchLine = "/mul_",
        };

        Assert.DoesNotContain("feature/x", StatusText(model), StringComparison.Ordinal);
    }

    private static string StatusText(ScreenModel model) =>
        string.Concat(ChromeBuilder.Status(model, 160, GraphGlyphSet.Unicode).Segments.Select(segment => segment.Text));
}
