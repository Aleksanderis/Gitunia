using Gitunia.Core.Status;
using Gitunia.Git.Merge;
using Gitunia.Tui.App.Operations;

namespace Gitunia.Tui.App.Merge;

/// <summary>The status line for each way a merge can end. Pure, so every text is testable without a terminal.</summary>
public static class MergeTexts
{
    public static OperationReport Report(MergeResult result, MergeMode mode, string branch, string target)
    {
        var tip = OperationTexts.ShortHash(result.Head);
        var was = OperationTexts.ShortHash(result.OldHead);
        var undo = $"undo: git reset --hard {was} (or ORIG_HEAD, the reflog)";
        return result.Kind switch
        {
            MergeKind.UpToDate => new OperationReport(
                $"Already up to date: {branch} contains {target}",
                StatusSeverity.Info),
            MergeKind.FastForward => new OperationReport(
                $"Fast-forwarded {branch} to {tip} ({target}), was {was}; {undo}",
                StatusSeverity.Info),
            MergeKind.MergeCommit => new OperationReport(
                $"Merged {target} into {branch} ({tip}), was {was}; {undo}",
                StatusSeverity.Info),
            MergeKind.Squashed => new OperationReport(
                $"Squashed {target} into the index of {branch}: the changes are staged, commit them (<space>s); " +
                "nothing is committed yet",
                StatusSeverity.Info),
            _ => ConflictReport(mode, branch, target),
        };
    }

    public static string Failure(string branch, string target, string reason) =>
        $"Could not merge {target} into {branch}: {reason}";

    private static OperationReport ConflictReport(MergeMode mode, string branch, string target)
    {
        if (mode == MergeMode.Squash)
        {
            return new OperationReport(
                $"The squash of {target} stopped on conflicts: resolve the files marked !, then commit " +
                "(git reset --merge undoes it; no operation is in progress)",
                StatusSeverity.Warning);
        }

        return new OperationReport(
            $"The merge of {target} into {branch} stopped on conflicts: resolve the files marked !, " +
            "then <space>gc to continue or <space>ga to abort",
            StatusSeverity.Warning);
    }
}
