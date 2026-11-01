using Gitunia.Core.Stash;
using Gitunia.Core.Status;
using Gitunia.Git.Commits.Models;
using Gitunia.Git.Stash;
using Gitunia.Git.Stash.Models;
using Xunit;

namespace Gitunia.Core.Tests.Stash;

[Trait("Os", "Independent")]
public class StashReportsTests
{
    private static readonly StashEntry Entry = new(0, "stash@{0}", "abc123", "On main: \"quoted\" work");

    [Fact]
    public void GivenNothingToStash_WhenPushingWithoutUntracked_ThenTheWarningPointsAtTheUntrackedKey()
    {
        var report = StashReports.Push(new StashResult(StashOutcome.NothingToStash, null), string.Empty, false);

        Assert.Equal(StatusSeverity.Warning, report.Severity);
        Assert.Equal("Nothing to stash (untracked files are stashed with <space>ku)", report.Text);
    }

    [Fact]
    public void GivenNothingToStash_WhenPushingWithUntracked_ThenThereIsNoHint()
    {
        var report = StashReports.Push(new StashResult(StashOutcome.NothingToStash, null), string.Empty, true);

        Assert.Equal("Nothing to stash", report.Text);
    }

    [Fact]
    public void GivenAMessage_WhenPushed_ThenTheReportNamesItAndTheShortHash()
    {
        var hash = new string('a', 40);

        var report = StashReports.Push(new StashResult(StashOutcome.Done, hash), "wip", false);

        Assert.Equal(StatusSeverity.Info, report.Severity);
        Assert.StartsWith("Stashed \"wip\" as aaaa", report.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenAConflictingPop_WhenReporting_ThenTheWarningSaysTheStashIsKept()
    {
        var report = StashReports.Apply(new StashResult(StashOutcome.Conflicted, null), Entry, true);

        Assert.Equal(StatusSeverity.Warning, report.Severity);
        Assert.Contains("with conflicts", report.Text, StringComparison.Ordinal);
        Assert.Contains("stash is kept", report.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenACleanPop_WhenReporting_ThenTheMessageCarriesTheRestoreCommand()
    {
        var report = StashReports.Apply(new StashResult(StashOutcome.Done, null), Entry, true);

        Assert.Equal(
            "Popped stash@{0}; undo the drop: git stash store -m \"On main: \\\"quoted\\\" work\" abc123",
            report.Text);
    }

    [Fact]
    public void GivenApply_WhenReporting_ThenItSaysTheStashIsKept()
    {
        var report = StashReports.Apply(new StashResult(StashOutcome.Done, null), Entry, false);

        Assert.Equal("Applied stash@{0}; the stash is kept", report.Text);
    }

    [Fact]
    public void GivenTheEntryIsGone_WhenApplyingOrDropping_ThenItWarnsAndSaysNothingChanged()
    {
        var missing = new StashResult(StashOutcome.Missing, null);

        foreach (var report in new[] { StashReports.Apply(missing, Entry, false), StashReports.Drop(missing, Entry) })
        {
            Assert.Equal(StatusSeverity.Warning, report.Severity);
            Assert.Equal("stash@{0} is no longer in the stash list: nothing was changed", report.Text);
        }
    }

    [Fact]
    public void GivenADrop_WhenReporting_ThenTheMessageHasHashAndRestoreCommand()
    {
        var report = StashReports.Drop(new StashResult(StashOutcome.Done, null), Entry);

        Assert.StartsWith("Dropped stash@{0} (abc123); undo: git stash store -m", report.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenARestoreWithABackup_WhenReporting_ThenTheMessageHasTheUndoCommand()
    {
        var file = new StashFileChange(
            new CommitFileChange("a b.txt", null, FileChangeKind.Modified, null),
            StashFileOrigin.Tracked);
        var target = new StashFileTarget(Entry, file);

        var withBackup = StashReports.Restore(new StashRestoreResult(StashRestoreState.Overwrites, "deadbeef"), target);
        var without = StashReports.Restore(new StashRestoreResult(StashRestoreState.Creates, null), target);

        Assert.Equal(
            "Restored a b.txt from stash@{0}; undo: git cat-file blob deadbeef > \"a b.txt\"",
            withBackup.Text);
        Assert.Equal("Restored a b.txt from stash@{0}", without.Text);
    }
}
