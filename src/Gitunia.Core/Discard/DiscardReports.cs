using Gitunia.Core.Staging;
using Gitunia.Core.Status;
using Gitunia.Core.Text;
using Gitunia.Git.Staging.Models;
using Gitunia.Git.Status.Models;

namespace Gitunia.Core.Discard;

/// <summary>What the status bar says when a discard ended.</summary>
public static class DiscardReports
{
    /// <summary>The report of whole files discarded in one batch.</summary>
    public static string Files(IReadOnlyList<StatusEntry> files, int skipped)
    {
        var deleted = files.Count(entry => entry.Kind == StatusEntryKind.Untracked);
        List<string> extra = [];
        if (deleted > 0)
        {
            extra.Add($"{deleted} untracked deleted");
        }

        if (skipped > 0)
        {
            extra.Add($"{skipped} skipped");
        }

        var suffix = extra.Count > 0 ? $" ({string.Join(", ", extra)})" : string.Empty;

        return $"Discarded {CountText.Plural(files.Count, "file")}{suffix}";
    }

    /// <summary>The failure of whole files: git may have restored the tracked ones before it failed.</summary>
    public static string FilesFailure(string reason) =>
        $"Could not discard: {reason} (the tracked files may already be restored; the status is read again)";

    /// <summary>The report of one file, a hunk or lines.</summary>
    public static string Selection(StagingSelection selection, PatchStagingResult result)
    {
        var name = PrintableText.Of(selection.Entry.Path);
        var isUntracked = selection.Entry.Kind == StatusEntryKind.Untracked;

        return result.Outcome switch
        {
            PatchStagingOutcome.Applied => $"Discarded {Describe(selection, result)} of {name}",
            PatchStagingOutcome.WholeFile when isUntracked => $"Deleted untracked {name}",
            PatchStagingOutcome.WholeFile when selection.Lines.Count == 0 => $"Discarded all changes to {name}",
            PatchStagingOutcome.WholeFile => $"Discarded all changes to {name}: it has no hunks to pick",
            PatchStagingOutcome.NotSplittable =>
                $"{name} cannot be split into hunks or lines (binary, not valid UTF-8 or renamed): nothing was "
                + "discarded; discard the whole file from the list",
            PatchStagingOutcome.FileChanged =>
                $"{name} changed since the diff was read: nothing was discarded, the diff is read again",
            _ => "Nothing to discard in the selection",
        };
    }

    /// <summary>The failure of one file, a hunk or lines.</summary>
    public static string SelectionFailure(string reason) => $"Could not discard: {reason}";

    /// <summary>How serious a report of one file, a hunk or lines is.</summary>
    public static StatusSeverity SelectionSeverity(PatchStagingResult result)
    {
        if (result.Outcome is PatchStagingOutcome.FileChanged or PatchStagingOutcome.NotSplittable)
        {
            return StatusSeverity.Warning;
        }

        return StatusSeverity.Info;
    }

    /// <summary>The change was made (the diff cursor may follow).</summary>
    public static bool IsDone(PatchStagingResult result) =>
        result.Outcome is PatchStagingOutcome.Applied or PatchStagingOutcome.WholeFile;

    private static string Describe(StagingSelection selection, PatchStagingResult result)
    {
        var lines = CountText.Plural(result.LineCount, "line");

        return selection.IsRange ? lines : $"change ({lines})";
    }
}
