using Gitunia.Core.Keymap;
using Gitunia.Core.Status;
using Gitunia.Core.Text;
using Gitunia.Git.Stash;
using Gitunia.Git.Stash.Models;

namespace Gitunia.Core.Stash;

/// <summary>What the status bar says when a stash operation ended, and the undo command it names.</summary>
public static class StashReports
{
    /// <summary>The git command that brings a dropped stash back.</summary>
    public static string RestoreCommand(StashEntry entry) =>
        $"git stash store -m \"{entry.Message.Replace("\"", "\\\"", StringComparison.Ordinal)}\" {entry.CommitHash}";

    /// <summary>The report of a push.</summary>
    public static StashReport Push(StashResult result, string message, bool includeUntracked)
    {
        if (result.Outcome == StashOutcome.NothingToStash)
        {
            var hint = includeUntracked ? string.Empty : $" (untracked files are stashed with {KeyTokens.Of(ActionIds.StashPushUntracked, "stash with untracked files")})";
            return new StashReport($"Nothing to stash{hint}", StatusSeverity.Warning);
        }

        var named = message.Length > 0 ? $" \"{message}\"" : string.Empty;
        return new StashReport($"Stashed{named} as {ShortHash.Of(result.CommitHash)}", StatusSeverity.Info);
    }

    /// <summary>The report of an apply or a pop.</summary>
    public static StashReport Apply(StashResult result, StashEntry entry, bool pop)
    {
        if (result.Outcome == StashOutcome.Missing)
        {
            return Gone(entry);
        }

        if (result.Outcome == StashOutcome.Conflicted)
        {
            return new StashReport(
                $"Applied {entry.Name} with conflicts: the files are marked !, the stash is kept",
                StatusSeverity.Warning);
        }

        if (pop)
        {
            return new StashReport(
                $"Popped {entry.Name}",
                StatusSeverity.Info,
                UndoNotes.Restore(RestoreCommand(entry)));
        }

        return new StashReport($"Applied {entry.Name}; the stash is kept", StatusSeverity.Info);
    }

    /// <summary>The report of a drop.</summary>
    public static StashReport Drop(StashResult result, StashEntry entry)
    {
        if (result.Outcome == StashOutcome.Missing)
        {
            return Gone(entry);
        }

        return new StashReport(
            $"Dropped {entry.Name} ({ShortHash.Of(entry.CommitHash)})",
            StatusSeverity.Info,
            UndoNotes.Restore(RestoreCommand(entry)));
    }

    /// <summary>The report of a restore, or of a check that found nothing to restore.</summary>
    public static StashReport Restore(StashRestoreResult result, StashFileTarget target)
    {
        var path = target.File.Change.Path;
        var name = target.Entry.Name;
        return result.State switch
        {
            StashRestoreState.Missing => Gone(target.Entry),
            StashRestoreState.NotInStash => new StashReport(
                $"{name} holds no copy of {path} to restore (the stash deletes it): nothing was changed",
                StatusSeverity.Warning),
            StashRestoreState.Identical => new StashReport(
                $"{path} already matches {name}: nothing to restore",
                StatusSeverity.Info),
            _ => Restored(result, path, name),
        };
    }

    private static StashReport Restored(StashRestoreResult result, string path, string name)
    {
        var text = $"Restored {path} from {name}";
        if (result.BackupBlob is null)
        {
            return new StashReport(text, StatusSeverity.Info);
        }

        return new StashReport(
            text,
            StatusSeverity.Info,
            UndoNotes.Undo($"git cat-file blob {result.BackupBlob} > \"{path}\""));
    }

    private static StashReport Gone(StashEntry entry) =>
        new($"{entry.Name} is no longer in the stash list: nothing was changed", StatusSeverity.Warning);
}
