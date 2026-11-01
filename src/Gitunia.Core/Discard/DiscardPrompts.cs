using Gitunia.Core.Overlays;
using Gitunia.Core.Staging;
using Gitunia.Core.Text;
using Gitunia.Git.Commits.Models;
using Gitunia.Git.Status.Models;

namespace Gitunia.Core.Discard;

/// <summary>The words of the discard dialogs. Each names exactly what will be thrown away and says that git keeps no copy
/// (ux-workflows.md, workflow 2, "Discard").</summary>
public static class DiscardPrompts
{
    /// <summary>The suffix git gives an untracked directory.</summary>
    public const string DirectorySuffix = "/";

    private const string TrackedConsequence =
        "The changes are removed from the file on disk. Git keeps no copy: this cannot be undone.";

    private const string RangeConsequence =
        "The changes are removed from disk and untracked files are deleted. This cannot be undone.";

    private const string UntrackedConsequence =
        "It is removed from disk. Git has no copy of it: this cannot be undone.";

    private const int NamedUntrackedLimit = 5;

    /// <summary>The dialog for one file.</summary>
    public static ConfirmPrompt ForFile(StatusEntry entry)
    {
        var name = PrintableText.Of(entry.Path);
        if (entry.Kind == StatusEntryKind.Untracked)
        {
            var isDirectory = entry.Path.EndsWith(DirectorySuffix, StringComparison.Ordinal);
            var kind = isDirectory ? "directory" : "file";
            var everything = isDirectory ? " and everything in it" : string.Empty;

            return new ConfirmPrompt(
                $"Delete untracked {kind}",
                [$"Delete the untracked {kind} {name}{everything}?"],
                UntrackedConsequence,
                $"Delete {kind}");
        }

        List<string> lines = [$"Discard all unstaged changes to {name}?"];
        if (entry.WorktreeChange == FileChangeKind.Deleted)
        {
            lines.Add("The deleted file is restored from the index.");
        }

        if (entry.IndexChange != FileChangeKind.Unmodified)
        {
            lines.Add("Its staged changes are kept.");
        }

        return new ConfirmPrompt("Discard changes", lines, TrackedConsequence, "Discard changes");
    }

    /// <summary>The dialog for several files: it counts them and names the untracked ones, which are deleted.</summary>
    /// <param name="files">The files that will be discarded.</param>
    /// <param name="skipped">How many picked files are left out.</param>
    /// <param name="folderName">The folder the files are under, null for a Visual range.</param>
    public static ConfirmPrompt ForFiles(IReadOnlyList<StatusEntry> files, int skipped, string? folderName)
    {
        var untracked = files.Where(entry => entry.Kind == StatusEntryKind.Untracked).ToList();
        var subject = CountText.Plural(files.Count, "file");
        var question = $"Discard changes to {subject}?";
        if (folderName is not null)
        {
            question = $"Discard changes to {subject} in {folderName}?";
        }

        List<string> lines = [question];
        if (untracked.Count > 0)
        {
            var names = untracked.Take(NamedUntrackedLimit).Select(entry => PrintableText.Of(entry.Path)).ToList();
            var more = untracked.Count > names.Count ? $" and {untracked.Count - names.Count} more" : string.Empty;
            lines.Add($"Untracked, deleted from disk ({untracked.Count}): {string.Join(", ", names)}{more}");
            if (untracked.Any(entry => entry.Path.EndsWith(DirectorySuffix, StringComparison.Ordinal)))
            {
                lines.Add("Untracked directories are deleted with everything in them.");
            }
        }

        if (files.Any(entry => entry.Kind != StatusEntryKind.Untracked && entry.IndexChange != FileChangeKind.Unmodified))
        {
            lines.Add("Staged changes are kept.");
        }

        if (skipped > 0)
        {
            lines.Add($"{CountText.Plural(skipped, "conflicted, submodule or unreadable-name file")} left out.");
        }

        return new ConfirmPrompt(
            "Discard changes",
            lines,
            RangeConsequence,
            $"Discard {CountText.Plural(files.Count, "file")}");
    }

    /// <summary>The dialog for the hunk or Visual lines of a diff.</summary>
    public static ConfirmPrompt ForSelection(StagingSelection selection)
    {
        var entry = selection.Entry;
        if (entry.Kind == StatusEntryKind.Untracked)
        {
            return ForFile(entry);
        }

        var name = PrintableText.Of(entry.Path);
        var count = selection.Lines.Count;
        if (selection.IsRange)
        {
            return new ConfirmPrompt(
                "Discard lines",
                [$"Discard the {CountText.Plural(count, "selected line")} of {name}?"],
                TrackedConsequence,
                "Discard lines");
        }

        if (NamesWholeFile(selection))
        {
            var reason = entry.WorktreeChange == FileChangeKind.Deleted ? "is deleted" : "has no hunks to pick";

            return new ConfirmPrompt(
                "Discard changes",
                [$"{name} {reason}. Discard all unstaged changes to it?"],
                TrackedConsequence,
                "Discard changes");
        }

        return new ConfirmPrompt(
            "Discard change",
            [$"Discard this change ({CountText.Plural(count, "line")}) of {name}?"],
            TrackedConsequence,
            "Discard change");
    }

    /// <summary>True when <see cref="ForSelection"/> words the dialog as discarding or deleting the whole file. Only then
    /// may the whole file go.</summary>
    public static bool NamesWholeFile(StagingSelection selection) =>
        selection.Entry.Kind == StatusEntryKind.Untracked
        || selection.Lines.Count == 0
        || IsWholeFileOnly(selection.Entry);

    /// <summary>A file that has no lines to pick: untracked or deleted in the working tree.</summary>
    public static bool IsWholeFileOnly(StatusEntry entry) =>
        entry.Kind == StatusEntryKind.Untracked || entry.WorktreeChange == FileChangeKind.Deleted;
}
