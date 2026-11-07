using Gitunia.Core.Operations;
using Gitunia.Core.Status;
using Gitunia.Core.Text;
using Gitunia.Git.Diff.Models;
using Gitunia.Git.Staging.Models;

namespace Gitunia.Core.Staging;

/// <summary>What the status bar says when a stage or unstage ended.</summary>
public static class StagingReports
{
    /// <summary>Continuing an operation in progress and its key, named in the resolved report.</summary>
    public static readonly string Continue = OperationKeys.Continue;

    /// <summary>The report of whole files staged or unstaged.</summary>
    public static string Paths(StagingPlan plan, bool isStage)
    {
        if (plan.IsSavedResolve)
        {
            return $"Resolved and staged {plan.Name}";
        }

        if (plan.IsResolve)
        {
            return $"Marked {plan.Name} resolved";
        }

        var verb = isStage ? "Staged" : "Unstaged";
        var what = $"{plan.Count} files";
        if (plan.Name.Length > 0)
        {
            what = plan.Name;
        }
        else if (plan.Count == 1)
        {
            what = "1 file";
        }

        return plan.Skipped > 0 ? $"{verb} {what} ({plan.Skipped} conflicted skipped)" : $"{verb} {what}";
    }

    /// <summary>The failure of whole files.</summary>
    public static string PathsFailure(bool isStage, string reason) =>
        $"Could not {(isStage ? "stage" : "unstage")}: {reason}";

    /// <summary>The report of a hunk or lines.</summary>
    public static string Selection(StagingSelection selection, PatchStagingResult result)
    {
        var isStage = selection.Side == WorkingDiffSide.Unstaged;
        var verb = isStage ? "stage" : "unstage";
        var past = isStage ? "Staged" : "Unstaged";
        var name = PrintableText.Of(selection.Entry.Path);
        var lines = CountText.Plural(result.LineCount, "line");
        var what = selection.IsRange ? lines : $"change ({lines})";

        return result.Outcome switch
        {
            PatchStagingOutcome.Applied => $"{past} {what} of {name}",
            PatchStagingOutcome.WholeFile =>
                $"{past} {name} whole: new, deleted and binary files are not split into hunks",
            PatchStagingOutcome.NotSplittable =>
                $"{name} cannot be split into lines (new, deleted or binary file): {verb} its change or the whole file",
            PatchStagingOutcome.FileChanged =>
                $"{name} changed since the diff was read: nothing was {verb}d, the diff is read again",
            _ => $"Nothing to {verb} in the selection",
        };
    }

    /// <summary>How serious a hunk or lines report is.</summary>
    public static StatusSeverity SelectionSeverity(PatchStagingResult result)
    {
        if (result.Outcome is PatchStagingOutcome.FileChanged or PatchStagingOutcome.NotSplittable)
        {
            return StatusSeverity.Warning;
        }

        return StatusSeverity.Info;
    }

    /// <summary>The change was made (the cursor and the commit that waited may follow).</summary>
    public static bool IsDone(PatchStagingResult result) =>
        result.Outcome is PatchStagingOutcome.Applied or PatchStagingOutcome.WholeFile;

    /// <summary>The failure of a hunk or lines.</summary>
    public static string SelectionFailure(StagingSelection selection, string reason)
    {
        var verb = selection.Side == WorkingDiffSide.Unstaged ? "stage" : "unstage";

        return $"Could not {verb} the selection: {reason}";
    }
}
