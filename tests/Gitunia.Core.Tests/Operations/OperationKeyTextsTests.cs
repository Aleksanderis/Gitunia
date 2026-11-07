using Gitunia.Git.Repositories.Models;
using Gitunia.Core.Commits;
using Gitunia.Core.Conflicts;
using Gitunia.Core.Editing;
using Gitunia.Core.Integration;
using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Operations;
using Gitunia.Core.Stash;
using Gitunia.Git.Operations.Models;
using Gitunia.Git.Stash.Models;
using Xunit;

namespace Gitunia.Core.Tests.Operations;

// The status, dialog and banner texts name the keys of the running keymap (#1198): the default keys as before, a
// remapped key when the user moved the action, no key when the action is unbound.
[Trait("Os", "Independent")]
public sealed class OperationKeyTextsTests
{
    private static readonly ActionId[] Named =
    [
        ActionIds.OperationContinue,
        ActionIds.OperationAbort,
        ActionIds.OperationSkip,
        ActionIds.StashPushUntracked,
        ActionIds.ToolMergetool,
        ActionIds.ToolCommitEditor,
        ActionIds.WorkingTreeUnstageAll,
        ActionIds.ConflictOpen,
        ActionIds.WorkingTreeStage,
    ];

    private static readonly string[] RemappedKeys = ["zc", "za", "zs", "zk", "zm", "ze", "zu", "zr", "zg"];

    public static TheoryData<string, string, string> Texts => new()
    {
        { OperationKeys.InProgress("merge"), "continue (<space>gc) or abort (<space>ga)", "continue (<space>zc) or abort (<space>za)" },
        { OperationTexts.StillConflicted(2), "stage (<space>a)", "stage (<space>zg)" },
        {
            StashReports.Push(new StashResult(StashOutcome.NothingToStash, null), string.Empty, false).Text,
            "stash with untracked files (<space>ku)",
            "stash with untracked files (<space>zk)"
        },
        { ConflictNotices.MergetoolHint, "the mergetool (<space>em)", "the mergetool (<space>zm)" },
        { CommitFailureReport.Hint, "the commit editor key (<space>ec)", "the commit editor key (<space>ze)" },
        { CommitTexts.StageAll.Consequence, "unstage all (<space>U)", "unstage all (<space>zu)" },
    };

    [Theory]
    [MemberData(nameof(Texts))]
    public void GivenTheDefaultAndARemappedKeymap_WhenResolvingAText_ThenItNamesTheKeyOfEach(string text, string asDefault, string asRemapped)
    {
        var defaults = new KeyBindingTable(DefaultKeymap.CreateBindings());

        Assert.Contains(asDefault, KeyTokens.Resolve(text, defaults), StringComparison.Ordinal);
        Assert.Contains(asRemapped, KeyTokens.Resolve(text, Remapped()), StringComparison.Ordinal);
    }

    [Fact]
    public void GivenUnboundActions_WhenResolvingTheInProgressRefusal_ThenNoKeyIsNamed()
    {
        var text = KeyTokens.Resolve(OperationKeys.InProgress("rebase"), new KeyBindingTable([]));

        Assert.Equal("A rebase is in progress: finish or abort the rebase first: continue or abort", text);
    }

    [Fact]
    public void GivenARemappedKeymap_WhenBuildingTheBanner_ThenTheChipsAndTheHintNameTheNewKeys()
    {
        var banner = OperationBannerBuilder.Build(Info(RepositoryOperation.Rebase), 2, keymap: Remapped())!;

        Assert.Equal(["<space>zc", "<space>za", "<space>zs"], banner.Chips.Select(chip => chip.Keys));
        Assert.Equal("<space>zr open conflict editor", banner.Hint);
        Assert.Equal("<space>zr conflict editor", banner.ShortHint);
    }

    [Fact]
    public void GivenARemappedMergetool_WhenTheEditorTakesNoFile_ThenTheHintNamesTheNewKey()
    {
        var banner = OperationBannerBuilder.Build(Info(RepositoryOperation.Merge), 1, editorOpens: false, keymap: Remapped())!;

        Assert.Equal("<space>zm open mergetool", banner.Hint);
    }

    [Fact]
    public void GivenUnboundActions_WhenBuildingTheBanner_ThenTheHintIsLeftOutAndTheChipsHaveNoKeys()
    {
        var banner = OperationBannerBuilder.Build(Info(RepositoryOperation.Merge), 1, keymap: new KeyBindingTable([]))!;

        Assert.Equal(string.Empty, banner.Hint);
        Assert.All(banner.Chips, chip => Assert.Equal(string.Empty, chip.Keys));
    }

    private static OperationInfo Info(RepositoryOperation kind) => new(kind, "x", null, null, null, null, null, null);

    private static KeyBindingTable Remapped()
    {
        var bindings = Named.Select((action, index) => new KeyBinding(
            KeyMode.Normal,
            KeyBindingTable.GlobalView,
            KeySequenceParser.Parse($"<space>{RemappedKeys[index]}"),
            action));

        return new KeyBindingTable([.. bindings]);
    }
}
