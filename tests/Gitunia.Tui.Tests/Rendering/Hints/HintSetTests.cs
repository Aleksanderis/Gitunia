using Gitunia.Core.Config.Models;
using Gitunia.Core.Editing;
using Gitunia.Core.Hints;
using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Keymap.Repeat;
using Gitunia.Core.Text;
using Gitunia.Core.Workspaces.Panes;
using Gitunia.Git.History.Models;
using Gitunia.Tui.Rendering.Graph;
using Gitunia.Tui.Rendering.Screen;
using Xunit;

namespace Gitunia.Tui.Tests.Rendering.Hints;

public class HintSetTests
{
    public static TheoryData<PaneKind> Panes() => [.. Enum.GetValues<PaneKind>()];

    // Keys that mean the same on every screen: their label is one word of the closed vocabulary.
    // Enter on a conflicted file names the editor it opens; the label is shared with WithConflictEditor.
    private const string ConflictEditorLabel = "conflict editor";

    private static readonly string[] SharedKeys = ["Esc", "Enter", "j/k", "Ctrl-g", "Ctrl-s", "n/Esc", "Ctrl-j/k"];

    [Fact]
    public void GivenEverySetOfHints_WhenReadingTheSharedKeys_ThenTheirLabelIsAHintVerb()
    {
        var sets = new List<IReadOnlyList<KeyHint>>
        {
            HintSet.ForDetailChanges(), HintSet.ForGraphComparing(), HintSet.ForMessageEditing(),
            HintSet.ForConfirm(), HintSet.ForBlameList(), HintSet.ForFileHistory(), HintSet.ForReflogList(),
            HintSet.ForRebaseEditor(), HintSet.ForConflictVersions(), HintSet.ForConflictVisual(),
            HintSet.ForConflictResult(), HintSet.ForConflictEdit(), HintSet.ForRebaseMessage(),
            HintSet.ForPalette(), HintSet.ForRepeat(RepeatKind.Context),
            HintSet.ForRepeat(RepeatKind.Resize), HintSet.ForLineHistory(new LineRange(1, 2)),
        };
        foreach (var pane in Enum.GetValues<PaneKind>())
        {
            sets.Add(HintSet.For(pane));
            sets.Add(HintSet.For(pane, showsFileContent: true));
            sets.Add(HintSet.For(pane, showsFileContent: false, onConflict: true));
            sets.Add(HintSet.For(pane, showsFileContent: false, onRefusedConflict: true));
        }

        var offenders = sets.SelectMany(set => set)
            .Where(hint => SharedKeys.Contains(hint.Keys) && !HintVerb.All.Contains(hint.Description))
            .Where(hint => hint.Description != ConflictEditorLabel)
            .Select(hint => $"{hint.Keys} {hint.Description}")
            .Distinct()
            .ToList();

        Assert.True(offenders.Count == 0, $"Not in HintVerb: {string.Join("; ", offenders)}");
    }

    [Theory]
    [MemberData(nameof(Panes))]
    public void GivenMinimalMode_WhenLimitingAPanesHints_ThenOnlyHelpCommandsAndQuitRemain(PaneKind pane)
    {
        var limited = HintSet.Limit(HintSet.For(pane), StatusBarHintMode.Minimal);

        Assert.Equal(["?", ":", "q"], limited.Select(hint => hint.Keys));
        Assert.All(limited, hint => Assert.Contains(HintSet.For(pane), other => other == hint));
    }

    [Theory]
    [MemberData(nameof(Panes))]
    public void GivenFullMode_WhenLimitingAtEachLevel_ThenOnlyHintsWithAListedActionRemainAndEveryoneKeepsThePinnedOnes(
        PaneKind pane)
    {
        var hints = HintSet.For(pane);

        foreach (var level in Enum.GetValues<CommandLevel>())
        {
            var limited = HintSet.Limit(hints, StatusBarHintMode.Full, level);

            Assert.Equal(["?", ":", "q"], limited.Take(3).Select(hint => hint.Keys));
            Assert.All(limited, hint => Assert.True(HintSet.IsListedAt(hint, level)));
            Assert.Equal(hints.Count(hint => HintSet.IsListedAt(hint, level)), limited.Count);
        }
    }

    [Theory]
    [InlineData(PaneKind.Detail, "<space>gb")]
    [InlineData(PaneKind.Detail, "<space>H")]
    [InlineData(PaneKind.WorkingUnstaged, "<space>gb")]
    [InlineData(PaneKind.WorkingUnstaged, "<space>H")]
    [InlineData(PaneKind.WorkingStaged, "<space>gb")]
    [InlineData(PaneKind.WorkingStaged, "<space>H")]
    [InlineData(PaneKind.Diff, "<space>L")]
    [InlineData(PaneKind.WorkingUnstagedDiff, "<space>L")]
    [InlineData(PaneKind.WorkingStagedDiff, "<space>L")]
    public void GivenAPaneWhereHistoryOrBlameWorks_WhenListingItsHints_ThenTheKeyIsShownAtDefaultLevel(
        PaneKind pane,
        string keys)
    {
        var hints = HintSet.Limit(HintSet.For(pane), StatusBarHintMode.Full, CommandLevel.Everyday);

        Assert.Contains(hints, hint => hint.Keys == keys);
    }

    [Fact]
    public void GivenALineRange_WhenAskingForLineHistoryHints_ThenTheFirstHintNamesTheRange()
    {
        var hints = HintSet.ForLineHistory(new LineRange(12, 20));

        Assert.Equal("Lines 12-20", hints[0].Keys);
        Assert.Equal(HintSet.ForFileHistory().Count + 1, hints.Count);
        Assert.Equal("Lines 7", HintSet.ForLineHistory(new LineRange(7, 7))[0].Keys);
    }

    [Fact]
    public void GivenTheMessageBoxHints_WhenLimitingByLevel_ThenTheEditorHintAppearsOnlyAtItsLevel()
    {
        var hints = HintSet.For(PaneKind.WorkingMessage);
        var editorLevel = ActionCatalog.LevelOf(ActionIds.ToolCommitEditor)!.Value;

        Assert.NotEqual(CommandLevel.Everyday, editorLevel);
        Assert.DoesNotContain(
            HintSet.Limit(hints, StatusBarHintMode.Full, CommandLevel.Everyday),
            hint => hint.Actions.Contains(ActionIds.ToolCommitEditor));
        Assert.Contains(
            HintSet.Limit(hints, StatusBarHintMode.Full, editorLevel),
            hint => hint.Actions.Contains(ActionIds.ToolCommitEditor));
    }

    [Fact]
    public void GivenAnAboveLevelHint_WhenLimitingToMinimalOrOff_ThenTheModeStillWins()
    {
        var hints = HintSet.For(PaneKind.WorkingMessage);

        Assert.Equal(3, HintSet.Limit(hints, StatusBarHintMode.Minimal).Count);
        Assert.Empty(HintSet.Limit(hints, StatusBarHintMode.Off));
    }

    [Fact]
    public void GivenOffMode_WhenLimitingHints_ThenNoneRemain()
    {
        Assert.Empty(HintSet.Limit(HintSet.For(PaneKind.Graph), StatusBarHintMode.Off));
    }

    [Fact]
    public void GivenFullMode_WhenLimitingHints_ThenTheyAreUnchanged()
    {
        var hints = HintSet.For(PaneKind.Graph);

        Assert.Same(hints, HintSet.Limit(hints, StatusBarHintMode.Full));
    }

    [Theory]
    [InlineData(PaneKind.Graph, "gg/G")]
    [InlineData(PaneKind.Sidebar, "/")]
    [InlineData(PaneKind.Detail, "Enter")]
    [InlineData(PaneKind.Diff, "<space>v")]
    [InlineData(PaneKind.WorkingUnstaged, "<space>c")]
    [InlineData(PaneKind.WorkingStaged, "<space>c")]
    [InlineData(PaneKind.WorkingUnstagedDiff, "]c/[c")]
    [InlineData(PaneKind.WorkingStagedDiff, "<space>v")]
    public void GivenAPane_WhenAskingForHints_ThenTheyMentionThatPanesKeys(PaneKind pane, string expectedKeys)
    {
        Assert.Contains(HintSet.For(pane), hint => hint.Keys == expectedKeys);
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
        Assert.Contains(HintSet.For(pane), hint => hint.Keys == "<space>j" && hint.Actions.Contains(ActionIds.QuickJump));
    }

    [Theory]
    [InlineData(PaneKind.Graph)]
    [InlineData(PaneKind.Sidebar)]
    [InlineData(PaneKind.Detail)]
    public void GivenAHistoryPane_WhenAskingForHints_ThenTheyShowHowToOpenTheWorkingTree(PaneKind pane)
    {
        Assert.Contains(HintSet.For(pane), hint => hint.Keys == "<space>s" && hint.Description == "Changes view");
    }

    [Theory]
    [MemberData(nameof(Panes))]
    public void GivenAPane_WhenAskingForHints_ThenEveryHintHasKeysAndADescription(PaneKind pane)
    {
        Assert.NotEmpty(HintSet.For(pane));

        foreach (var hint in HintSet.For(pane))
        {
            Assert.False(string.IsNullOrWhiteSpace(hint.Keys), "A hint has no keys.");
            Assert.False(string.IsNullOrWhiteSpace(hint.Description), $"The hint {hint.Keys} has no description.");
        }
    }

    [Fact]
    public void GivenAMarkedCommit_WhenAskingForGraphHints_ThenTheyNameMovingTheMarkAndEndingTheCompare()
    {
        var hints = HintSet.ForGraphComparing();

        Assert.Contains(hints, hint => hint.Keys == "<space>=" && hint.Description == "move mark");
        Assert.Contains(hints, hint => hint.Keys == "Esc" && hint.Description == HintVerb.Cancel);
        Assert.Contains(hints, hint => hint.Keys == "gg/G");
        AssertKeysTriggerActions(hints, KeyMode.Normal, DefaultKeymap.GraphView);
    }

    [Fact]
    public void GivenAFilesContentInTheDiffPane_WhenHintsAreAsked_ThenNoHunkFileOrDiffOptionHintsShow()
    {
        var hints = HintSet.For(PaneKind.Diff, showsFileContent: true);

        string[] diffOnly = ["hunk", "file", "context", "whole file", "side by side", "ignore space"];
        Assert.DoesNotContain(hints, hint => diffOnly.Contains(hint.Description));
        Assert.Contains(hints, hint => hint.Description == "copy file");
        Assert.Equal(HintSet.For(PaneKind.Diff), HintSet.For(PaneKind.Diff, showsFileContent: false));
    }

    [Fact]
    public void GivenAConflictedFileInTheUnstagedList_WhenAskingForHints_ThenTheEditorLeadsTheListsOwnHints()
    {
        var hints = HintSet.For(PaneKind.WorkingUnstaged, showsFileContent: false, onConflict: true);

        Assert.Equal("Enter", hints[3].Keys);
        Assert.Equal("conflict editor", hints[3].Description);
        Assert.DoesNotContain(hints, hint => hint.Description == "diff");
        Assert.DoesNotContain(HintSet.For(PaneKind.WorkingUnstaged), hint => hint.Description == "conflict editor");
        AssertKeysTriggerActions(hints, KeyMode.Normal, DefaultKeymap.WorkingUnstagedView);
    }

    [Fact]
    public void GivenAConflictedFileInItsDiff_WhenAskingForHints_ThenTheLeaderKeysOfTheEditorLeadAndAreBound()
    {
        var hints = HintSet.For(PaneKind.WorkingUnstagedDiff, showsFileContent: false, onConflict: true);

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
        var hints = HintSet.WithConflictEditor(HintSet.For(pane));

        Assert.Equal("<space>gr", hints[3].Keys);
        Assert.Equal("conflict editor", hints[3].Description);
        Assert.Equal(HintSet.For(pane).Count + 1, hints.Count);
        AssertKeysTriggerActions(hints, KeyMode.Normal, ViewFor(pane));
    }

    [Fact]
    public void GivenARefusedConflictedFileInTheUnstagedList_WhenAskingForHints_ThenTheMergetoolLeadsAndTheEditorIsNotOffered()
    {
        var hints = HintSet.For(PaneKind.WorkingUnstaged, showsFileContent: false, onRefusedConflict: true);

        Assert.Equal("<space>em", hints[3].Keys);
        Assert.Equal("mergetool", hints[3].Description);
        Assert.DoesNotContain(hints, hint => hint.Description is "conflict editor" or "diff");
        AssertKeysTriggerActions(hints, KeyMode.Normal, DefaultKeymap.WorkingUnstagedView);
    }

    [Fact]
    public void GivenARefusedConflictedFileInItsDiff_WhenAskingForHints_ThenTheMergetoolLeads()
    {
        var hints = HintSet.For(PaneKind.WorkingUnstagedDiff, showsFileContent: false, onRefusedConflict: true);

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
        var hints = HintSet.WithConflictMergetool(HintSet.For(pane));

        Assert.Equal("<space>em", hints[3].Keys);
        Assert.Equal("mergetool", hints[3].Description);
        Assert.Equal(HintSet.For(pane).Count + 1, hints.Count);
        AssertKeysTriggerActions(hints, KeyMode.Normal, ViewFor(pane));
    }

    [Fact]
    public void GivenHintsThatAlreadyLeadWithTheEditor_WhenAddingTheEditorHint_ThenTheyAreLeftAlone()
    {
        var hints = HintSet.For(PaneKind.WorkingUnstaged, showsFileContent: false, onConflict: true);

        Assert.Same(hints, HintSet.WithConflictEditor(hints));
    }

    [Theory]
    [InlineData(PaneKind.Graph)]
    [InlineData(PaneKind.WorkingStaged)]
    public void GivenAConflictedFileButAnotherPane_WhenAskingForHints_ThenTheyAreTheUsualOnes(PaneKind pane)
    {
        Assert.Equal(HintSet.For(pane), HintSet.For(pane, showsFileContent: false, onConflict: true));
    }

    [Theory]
    [MemberData(nameof(Panes))]
    public void GivenAPane_WhenAskingForHints_ThenNoKeyUsesTheCaretNotation(PaneKind pane)
    {
        Assert.All(HintSet.For(pane), hint => Assert.DoesNotContain('^', hint.Keys));
    }

    [Fact]
    public void GivenAFocusedDiffPane_WhenBuildingTheStatusBar_ThenItShowsKeysNextToTheirDescriptions()
    {
        var model = new ScreenModel("repo", null, 1, 1, "NORMAL", string.Empty, 1, 1, false, string.Empty, Hints: HintSet.For(PaneKind.Diff));

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
        var hints = HintSet.For(pane);

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

    [Theory]
    [MemberData(nameof(Panes))]
    public void GivenAPaneAt80Columns_WhenBuildingTheStatusBar_ThenHelpAndCommandsStayVisible(PaneKind pane)
    {
        var model = new ScreenModel("repo", null, 1, 1, "NORMAL", string.Empty, 1, 1, false, "Could not read the stashes: boom", Hints: HintSet.For(pane));

        var text = string.Concat(ChromeBuilder.Status(model, 80, GraphGlyphSet.Unicode).Segments.Select(segment => segment.Text));

        Assert.Contains("? help  : commands", text, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(Panes))]
    public void GivenAPane_WhenCheckingItsHints_ThenEveryKeyTriggersItsActionInTheDefaultKeymapForThatPane(PaneKind pane)
    {
        var view = ViewFor(pane);

        AssertKeysTriggerActions(HintSet.For(pane), KeyMode.Normal, view);
    }

    [Fact]
    public void GivenTheDetailChangesHints_WhenCheckingThem_ThenTheViewOptionsAreOnlyThereAndTheirKeysAreBound()
    {
        Assert.DoesNotContain(
            HintSet.For(PaneKind.Detail),
            hint => hint.Keys.StartsWith("<space>v", StringComparison.Ordinal));
        Assert.Contains(
            HintSet.ForDetailChanges(),
            hint => hint.Keys == "<space>v" && hint.Description == "view options");
        AssertKeysTriggerActions(HintSet.ForDetailChanges(), KeyMode.Normal, DefaultKeymap.DetailChangesView);
    }

    [Theory]
    [InlineData(RepeatKind.Context, RepeatMode.ContextView)]
    [InlineData(RepeatKind.Resize, RepeatMode.ResizeView)]
    public void GivenARepeatMode_WhenCheckingItsHints_ThenEveryKeyTriggersItsActionInTheModesView(
        RepeatKind kind,
        string view)
    {
        AssertKeysTriggerActions(HintSet.ForRepeat(kind), KeyMode.Normal, view);
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
            "Palette",
        ];

    [Theory]
    [MemberData(nameof(ModalHintSets))]
    public void GivenAModalHintSet_WhenCheckingItsHints_ThenEveryKeyTriggersItsActionInItsView(string set)
    {
        switch (set)
        {
            case "MessageEditing":
                AssertKeysTriggerActions(
                    HintSet.ForMessageEditing(), KeyMode.Insert, DefaultKeymap.WorkingMessageView);
                break;
            case "Confirm":
                AssertKeysTriggerActions(HintSet.ForConfirm(), KeyMode.Confirm, DefaultKeymap.ConfirmView);
                break;
            case "Reflog":
                AssertKeysTriggerActions(HintSet.ForReflogList(), KeyMode.Normal, DefaultKeymap.ReflogView);
                break;
            case "ConflictVersions":
                AssertKeysTriggerActions(
                    HintSet.ForConflictVersions(),
                    KeyMode.Normal,
                    DefaultKeymap.ConflictSideView);
                break;
            case "ConflictVisual":
                AssertKeysTriggerActions(
                    HintSet.ForConflictVisual(),
                    KeyMode.Visual,
                    DefaultKeymap.ConflictSideView);
                break;
            case "ConflictResult":
                AssertKeysTriggerActions(
                    HintSet.ForConflictResult(),
                    KeyMode.Normal,
                    DefaultKeymap.ConflictResultView);
                break;
            case "ConflictEdit":
                AssertKeysTriggerActions(
                    HintSet.ForConflictEdit(),
                    KeyMode.Insert,
                    DefaultKeymap.ConflictResultView);
                break;
            case "RebaseEditor":
                AssertKeysTriggerActions(HintSet.ForRebaseEditor(), KeyMode.Normal, DefaultKeymap.RebaseEditorView);
                break;
            case "RebaseMessage":
                AssertKeysTriggerActions(HintSet.ForRebaseMessage(), KeyMode.Insert, DefaultKeymap.RebaseEditorView);
                break;
            case "Palette":
                AssertKeysTriggerActions(HintSet.ForPalette(), KeyMode.Command, DefaultKeymap.PaletteView);
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
            SearchLine = new PromptLine("/", "mul", PromptCaret.End),
        };

        Assert.DoesNotContain("feature/x", StatusText(model), StringComparison.Ordinal);
    }

    private static string StatusText(ScreenModel model) =>
        string.Concat(ChromeBuilder.Status(model, 160, GraphGlyphSet.Unicode).Segments.Select(segment => segment.Text));
}
