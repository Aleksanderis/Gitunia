using Gitunia.Core.Diff;
using Gitunia.Git.Diff;
using Gitunia.Git.Commits.Models;
using Gitunia.Git.Diff.Models;
using Gitunia.Git.History.Models;
using Gitunia.Git.Status.Models;
using Xunit;

namespace Gitunia.Core.Tests.Diff;

[Trait("Os", "Independent")]
public class DiffLineRangesTests
{
    // Rows: 0 header, 1 hunk, 2 context a (1/1), 3 removed b (2/-), 4 added B (-/2), 5 added C (-/3), 6 context d (3/4).
    private const string TwoFilePatch =
        "diff --git a/f.txt b/f.txt\n--- a/f.txt\n+++ b/f.txt\n@@ -1,3 +1,4 @@\n a\n-b\n+B\n+C\n d\n"
        + "diff --git a/g.txt b/g.txt\n--- a/g.txt\n+++ b/g.txt\n@@ -1,1 +1,1 @@\n-x\n+y\n";

    [Fact]
    public void GivenARangeOverAddedAndContextRows_WhenTakingTheNewSide_ThenTheRangeSpansTheirNewLineNumbers()
    {
        var document = Build(TwoFilePatch);

        var picked = DiffLineRanges.Of(document, 2, 5, DiffNumbering.NewSide);

        Assert.True(picked.IsAccepted);
        Assert.Equal(new LineRange(1, 3), picked.Range);
        Assert.Equal(0, picked.FileIndex);
    }

    [Fact]
    public void GivenOnlyARemovedRow_WhenTakingTheNewSide_ThenItIsRefusedBecauseTheCommitNoLongerHasTheLine()
    {
        var document = Build(TwoFilePatch);

        var picked = DiffLineRanges.Of(document, 3, 3, DiffNumbering.NewSide);

        Assert.False(picked.IsAccepted);
        Assert.Equal(DiffLineRanges.RemovedNotice, picked.Notice);
    }

    [Fact]
    public void GivenOnlyAddedRows_WhenTakingTheOldSide_ThenItIsRefusedAsUncommitted()
    {
        var document = Build(TwoFilePatch);

        var picked = DiffLineRanges.Of(document, 4, 5, DiffNumbering.OldSide);

        Assert.False(picked.IsAccepted);
        Assert.Equal(DiffLineRanges.UncommittedNotice, picked.Notice);
    }

    [Fact]
    public void GivenARangeWithUncommittedLines_WhenTakingTheOldSide_ThenTheCommittedOnesAreUsedAndTheNoticeCountsTheRest()
    {
        var document = Build(TwoFilePatch);

        var picked = DiffLineRanges.Of(document, 2, 6, DiffNumbering.OldSide);

        Assert.Equal(new LineRange(1, 3), picked.Range);
        Assert.Equal("2 uncommitted lines are not in the history", picked.Notice);
    }

    [Fact]
    public void GivenAHeaderRow_WhenTakingLines_ThenNothingIsSelectedAndTheNoticeSaysSo()
    {
        var document = Build(TwoFilePatch);

        var picked = DiffLineRanges.Of(document, 0, 1, DiffNumbering.NewSide);

        Assert.False(picked.IsAccepted);
        Assert.Equal(DiffLineRanges.NoLineNotice, picked.Notice);
    }

    [Fact]
    public void GivenARangeReachingIntoASecondFile_WhenTakingLines_ThenOnlyTheFirstFileIsFollowedAndTheNoticeSaysSo()
    {
        var document = Build(TwoFilePatch);

        var picked = DiffLineRanges.Of(document, 2, document.Rows.Count - 1, DiffNumbering.NewSide);

        Assert.Equal(0, picked.FileIndex);
        Assert.Equal(new LineRange(1, 4), picked.Range);
        Assert.Equal(DiffLineRanges.FirstFileNotice, picked.Notice);
    }

    [Theory]
    [InlineData(StatusEntryKind.Untracked, WorkingDiffSide.Unstaged, false, DiffLineRanges.UntrackedNotice)]
    [InlineData(StatusEntryKind.Unmerged, WorkingDiffSide.Unstaged, false, DiffLineRanges.ConflictedNotice)]
    [InlineData(StatusEntryKind.Ordinary, WorkingDiffSide.Unstaged, true, DiffLineRanges.FolderNotice)]
    public void GivenAFileThatHasNoHeadHistory_WhenAskingForTheRefusal_ThenTheNoticeNamesWhy(
        StatusEntryKind kind,
        WorkingDiffSide side,
        bool isFolder,
        string expected)
    {
        var entry = new StatusEntry(kind, "f.txt", null, FileChangeKind.Unmodified, FileChangeKind.Modified, null, ConflictKind.None, null);

        Assert.Equal(expected, DiffLineRanges.RefusalFor(entry, side, isFolder));
    }

    [Fact]
    public void GivenAnUnstagedDiffOfAFileWithStagedChanges_WhenAskingForTheRefusal_ThenOnlyTheStagedDiffIsOffered()
    {
        var entry = new StatusEntry(
            StatusEntryKind.Ordinary, "f.txt", null, FileChangeKind.Modified, FileChangeKind.Modified, null, ConflictKind.None, null);

        Assert.Equal(DiffLineRanges.StagedTooNotice, DiffLineRanges.RefusalFor(entry, WorkingDiffSide.Unstaged, false));
        Assert.Null(DiffLineRanges.RefusalFor(entry, WorkingDiffSide.Staged, false));
    }

    [Fact]
    public void GivenAStagedRename_WhenAskingForTheHeadPath_ThenTheOldNameIsUsed()
    {
        var entry = new StatusEntry(
            StatusEntryKind.Renamed, "new.txt", "old.txt", FileChangeKind.Renamed, FileChangeKind.Unmodified, 100, ConflictKind.None, null);

        Assert.Equal("old.txt", DiffLineRanges.HeadPathOf(entry));
    }

    private static DiffDocument Build(string patch)
    {
        var builder = new DiffDocumentBuilder();
        foreach (var file in DiffParser.Parse(patch).Items)
        {
            builder.Add(file);
        }

        return builder.Build([]);
    }
}
