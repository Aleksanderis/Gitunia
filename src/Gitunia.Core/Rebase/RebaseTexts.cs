using Gitunia.Core.Overlays;
using Gitunia.Core.Status;
using Gitunia.Core.Text;
using Gitunia.Git.Operations.Models;
using Gitunia.Git.Refs.Models;

namespace Gitunia.Core.Rebase;

/// <summary>The words of the rebase onto a commit. Pure, so every text is testable without a terminal.</summary>
public static class RebaseTexts
{
    /// <summary>The confirm dialog: both sides, the flattened merges, the force-push warning and the undo.</summary>
    public static ConfirmPrompt Confirm(RebaseTarget target, GitRef? current, int merges, string? autoStashNote)
    {
        List<string> lines =
        [
            $"Rebase {target.BranchName} onto {target.Label}?",
            $"Its commits that {target.Label} does not have are replayed on top of {target.Label}; " +
            "they get new hashes.",
        ];
        if (merges > 0)
        {
            lines.Add(
                $"{RebaseCounts.Plural(merges, "merge commit")} in that range will be flattened: the merge " +
                "itself is dropped and the commits are replayed as one line. <space>bri keeps merges " +
                "(interactive rebase, --rebase-merges).");
        }

        if (RebaseGuard.UpstreamWarning(current) is { } warning)
        {
            lines.Add(warning);
        }

        if (autoStashNote is not null)
        {
            lines.Add(autoStashNote);
        }

        return new ConfirmPrompt(
            "Rebase",
            lines,
            $"{UndoNotes.FromReflog} A stop on conflicts leaves a " +
            "rebase in progress: <space>gc continues, <space>ga aborts.",
            "Rebase");
    }

    /// <summary>How the rebase ended.</summary>
    public static PostedText Report(RebaseTarget target, OperationResult result)
    {
        if (result.Outcome == OperationOutcome.Conflicted)
        {
            return new PostedText(
                $"Rebase of {target.BranchName} onto {target.Label} stopped on conflicts: resolve the files " +
                $"marked !, then <space>gc (continue), <space>ga (abort, back to " +
                $"{ShortHash.Of(target.OldTip)})",
                StatusSeverity.Warning);
        }

        if (string.Equals(result.Head, target.OldTip, StringComparison.Ordinal))
        {
            return new PostedText(
                $"{target.BranchName} is already based on {target.Label}: nothing changed",
                StatusSeverity.Info);
        }

        var head = ShortHash.Of(result.Head);
        var was = ShortHash.Of(target.OldTip);

        return new PostedText(
            $"Rebased {target.BranchName} onto {target.Label} ({head}), was {was}",
            StatusSeverity.Info,
            UndoNotes.Undo($"git reset --keep {target.OldTip} (or ORIG_HEAD, the reflog)"));
    }

    /// <summary>The failure line of a rebase that did not run.</summary>
    public static string Failure(RebaseTarget target, string reason) =>
        $"Could not rebase {target.BranchName} onto {target.Label}: {reason}";
}
