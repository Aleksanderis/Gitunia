using Gitunia.Core.Keymap;
using Gitunia.Core.Operations;
using Gitunia.Git.Operations.Models;
using Gitunia.Git.Repositories.Models;
using Xunit;

namespace Gitunia.Core.Tests.Operations;

public class OperationBannerBuilderTests
{
    private const string FullHash = "0123456789abcdef0123456789abcdef01234567";

    [Fact]
    public void GivenNoOperation_WhenBuilding_ThenThereIsNoBanner()
    {
        Assert.Null(OperationBannerBuilder.Build(null, 0));
        Assert.Null(OperationBannerBuilder.Build(Info(RepositoryOperation.Bisect), 0));
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(2, true)]
    public void GivenConflicts_WhenBuilding_ThenTheHintNamesTheEditorKeysOnlyWhileAnyRemain(int count, bool hinted)
    {
        var banner = OperationBannerBuilder.Build(Info(RepositoryOperation.Merge, target: "x"), count)!;

        Assert.Equal(hinted, banner.Hint.Contains("<space>gr open", StringComparison.Ordinal));
        Assert.Equal(hinted, banner.ShortHint.Contains("<space>gr", StringComparison.Ordinal));
    }

    [Fact]
    public void GivenConflictsTheEditorDoesNotTake_WhenBuilding_ThenTheHintNamesTheMergetoolNotTheEditor()
    {
        var banner = OperationBannerBuilder.Build(Info(RepositoryOperation.Rebase, target: "x"), 1, editorOpens: false)!;

        Assert.Equal("<space>em open mergetool", banner.Hint);
        Assert.Equal("<space>em mergetool", banner.ShortHint);
        Assert.DoesNotContain("<space>gr", banner.PlainText, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenNoConflictsLeft_WhenTheEditorDoesNotOpen_ThenThereIsNoHintAtAll()
    {
        var banner = OperationBannerBuilder.Build(Info(RepositoryOperation.Merge, target: "x"), 0, editorOpens: false)!;

        Assert.Equal(string.Empty, banner.Hint);
        Assert.Equal(string.Empty, banner.ShortHint);
    }

    [Fact]
    public void GivenAMerge_WhenBuilding_ThenTitleNamesTheBranchAndThereIsNoSkip()
    {
        var banner = OperationBannerBuilder.Build(Info(RepositoryOperation.Merge, target: "feature/x"), 3)!;

        Assert.Equal("MERGING feature/x", banner.Title);
        Assert.Equal("3 conflicts", banner.Detail);
        Assert.Equal(["continue", "abort"], banner.Chips.Select(chip => chip.Label));
        Assert.Equal(
            "MERGING feature/x  3 conflicts  <space>gc continue  <space>ga abort  "
            + "<space>gr open conflict editor",
            banner.PlainText);
    }

    [Fact]
    public void GivenAMergeWithoutAKnownBranch_WhenBuilding_ThenTheTitleIsJustTheVerb()
    {
        var banner = OperationBannerBuilder.Build(Info(RepositoryOperation.Merge), 1)!;

        Assert.Equal("MERGING", banner.Title);
        Assert.Equal("1 conflict", banner.Detail);
    }

    [Fact]
    public void GivenARebaseStep_WhenBuilding_ThenTheStepComesBeforeTheConflictsAndSkipIsOffered()
    {
        var info = Info(RepositoryOperation.Rebase, target: "topic") with { Step = 2, Total = 5 };

        var banner = OperationBannerBuilder.Build(info, 0)!;

        Assert.Equal("REBASING topic", banner.Title);
        Assert.Equal("step 2/5  no conflicts left", banner.Detail);
        Assert.Equal(["continue", "abort", "skip"], banner.Chips.Select(chip => chip.Label));
    }

    [Theory]
    [InlineData(RepositoryOperation.CherryPick, "CHERRY-PICKING 0123456")]
    [InlineData(RepositoryOperation.Revert, "REVERTING 0123456")]
    public void GivenAPickOrRevertRange_WhenBuilding_ThenTitleHasTheShortHashAndTheCommitsLeftAreCounted(
        RepositoryOperation kind,
        string title)
    {
        var info = Info(kind, commit: FullHash) with { Remaining = 3 };

        var banner = OperationBannerBuilder.Build(info, 1)!;

        Assert.Equal(title, banner.Title);
        Assert.Equal("3 commits left  1 conflict", banner.Detail);
    }

    [Fact]
    public void GivenASinglePick_WhenBuilding_ThenNoCountOfCommitsLeftIsShown()
    {
        var info = Info(RepositoryOperation.CherryPick, commit: FullHash) with { Remaining = 1 };

        Assert.Equal("1 conflict", OperationBannerBuilder.Build(info, 1)!.Detail);
    }

    [Fact]
    public void GivenEveryChip_WhenLookingUpItsKeys_ThenTheDefaultKeymapBindsThemToTheChipsAction()
    {
        var bindings = DefaultKeymap.CreateBindings();

        foreach (var chip in OperationBannerBuilder.Build(Info(RepositoryOperation.Rebase), 0)!.Chips)
        {
            var bound = bindings.Where(binding => binding.Action == chip.Action).Select(binding => binding.Sequence);
            Assert.Contains(KeySequenceParser.Parse(chip.Keys), bound);
        }
    }

    private static OperationInfo Info(RepositoryOperation kind, string? target = null, string? commit = null) =>
        new(kind, target, null, null, null, commit, null, null);
}
