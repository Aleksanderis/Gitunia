using Gitunia.Core.Operations;
using Gitunia.Core.Keymap;
using Gitunia.Core.Status;
using Gitunia.Core.Text;
using Gitunia.Git.Merge;

namespace Gitunia.Core.Integration;

/// <summary>The status line for each way a merge can end. Pure, so every text is testable without a terminal.</summary>
public static class MergeTexts
{
    public static IntegrationReport Report(MergeResult result, MergeMode mode, string branch, string target)
    {
        var tip = ShortHash.Of(result.Head);
        var was = ShortHash.Of(result.OldHead);
        var undo = UndoNotes.Undo($"git reset --hard {result.OldHead} (or ORIG_HEAD, the reflog)");
        return result.Kind switch
        {
            MergeKind.UpToDate => new IntegrationReport(
                $"Already up to date: {branch} contains {target}",
                StatusSeverity.Info),
            MergeKind.FastForward => new IntegrationReport(
                $"Fast-forwarded {branch} to {tip} ({target}), was {was}",
                StatusSeverity.Info,
                undo),
            MergeKind.MergeCommit => new IntegrationReport(
                $"Merged {target} into {branch} ({tip}), was {was}",
                StatusSeverity.Info,
                undo),
            MergeKind.Squashed => new IntegrationReport(
                $"Squashed {target} into the index of {branch}: the changes are staged, commit them in {KeyTokens.Of(ActionIds.WorkingTreeToggle, "the Changes view")}; " +
                "nothing is committed yet",
                StatusSeverity.Info),
            _ => ConflictReport(mode, branch, target),
        };
    }

    public static string Failure(string branch, string target, string reason) =>
        $"Could not merge {target} into {branch}: {reason}";

    private static IntegrationReport ConflictReport(MergeMode mode, string branch, string target)
    {
        if (mode == MergeMode.Squash)
        {
            return new IntegrationReport(
                $"The squash of {target} stopped on conflicts: resolve the files marked !, then commit " +
                "(no operation is in progress)",
                StatusSeverity.Warning,
                UndoNotes.Undo("git reset --merge"));
        }

        return new IntegrationReport(
            $"The merge of {target} into {branch} stopped on conflicts: resolve the files marked !, " +
            $"then {OperationKeys.Continue} or {OperationKeys.Abort}",
            StatusSeverity.Warning);
    }
}
