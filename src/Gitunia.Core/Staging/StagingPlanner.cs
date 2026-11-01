using Gitunia.Core.Lists;
using Gitunia.Core.Text;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Git.Diff.Models;
using Gitunia.Git.Staging;
using Gitunia.Git.Status.Models;

namespace Gitunia.Core.Staging;

/// <summary>Works out which paths a staging key touches, from the lists as they are on screen: the file under the
/// cursor, the files under a folder row, the files of a Visual range, or every file of a list. Conflicted files are
/// skipped everywhere but when one is marked resolved on its own.</summary>
public static class StagingPlanner
{
    private const string DirectorySuffix = "/";

    /// <summary>The plan of a key press for <paramref name="section"/> of <paramref name="tree"/>.</summary>
    public static StagingPlan For(WorkingTreeState tree, WorkingTreeSection section, bool isAll, RowRange? range)
    {
        if (isAll)
        {
            return All(tree, section);
        }

        if (range is { } rows)
        {
            return Range(tree, section, rows);
        }

        return One(tree, section);
    }

    /// <summary>Every file of the list but the conflicted ones.</summary>
    public static StagingPlan All(WorkingTreeState tree, WorkingTreeSection section)
    {
        var entries = tree.EntriesOf(section);
        var usable = entries.Where(entry => entry.Kind != StatusEntryKind.Unmerged).ToList();
        var skipped = entries.Count - usable.Count;
        if (usable.Count == 0)
        {
            return StagingPlan.Refused(
                skipped > 0 ? $"Only conflicted files are left ({skipped} skipped)" : "Nothing to do");
        }

        var paths = usable.SelectMany(entry => PathsOf(entry, section));

        return StagingPlan.For([.. paths], string.Empty, usable.Count, skipped);
    }

    /// <summary>The plan that stages one conflicted file as resolved.</summary>
    public static StagingPlan Resolve(StatusEntry entry, bool isSavedByEditor)
    {
        var name = PrintableText.Of(entry.Path);
        var plan = StagingPlan.For(PathsOf(entry, WorkingTreeSection.Unstaged), name, 1, 0);

        return isSavedByEditor ? plan with { IsSavedResolve = true } : plan with { IsResolve = true };
    }

    /// <summary>The conflicted file under the cursor of the Unstaged list, null when the cursor is elsewhere.</summary>
    public static StatusEntry? ConflictedUnderCursor(WorkingTreeState tree)
    {
        if (tree.Selection is not { Section: WorkingTreeSection.Unstaged } selection)
        {
            return null;
        }

        return tree.Unstaged.FirstOrDefault(entry =>
            entry.Path == selection.Path && entry.Kind == StatusEntryKind.Unmerged);
    }

    private static StagingPlan One(WorkingTreeState tree, WorkingTreeSection section)
    {
        if (tree.Selection is not { } selection || selection.Section != section)
        {
            return StagingPlan.Refused("Select a file first");
        }

        if (selection.IsFolder)
        {
            return Folder(tree, section, selection.Path);
        }

        var entry = tree.EntriesOf(section).FirstOrDefault(candidate => candidate.Path == selection.Path);
        if (entry is null)
        {
            return StagingPlan.Refused("Select a file first");
        }

        var name = PrintableText.Of(entry.Path);
        if (entry.Kind == StatusEntryKind.Unmerged)
        {
            return StagingPlan.Refused($"{name} is conflicted: mark it resolved from the Unstaged list");
        }

        return StagingPlan.For(PathsOf(entry, section), name, 1, 0);
    }

    // Every file under the folder, wherever it sits in the list; conflicted ones are skipped like in stage all.
    private static StagingPlan Folder(WorkingTreeState tree, WorkingTreeSection section, string folder)
    {
        var entries = tree.EntriesOf(section);
        var picked = tree.RowsOf(section).EntriesUnder(folder).Select(index => entries[index]).ToList();
        var name = $"{PrintableText.Of(folder)}{DirectorySuffix}";
        if (picked.Count == 0)
        {
            return StagingPlan.Refused($"{name} holds no files");
        }

        return Of(picked, section, name);
    }

    // The files of the Visual range (a folder row stands for all the files under it), once each; conflicted ones are
    // skipped like in stage all.
    private static StagingPlan Range(WorkingTreeState tree, WorkingTreeSection section, RowRange range)
    {
        var entries = tree.EntriesOf(section);
        var rows = tree.RowsOf(section);
        if (rows.Count == 0)
        {
            return StagingPlan.Refused("Select a file first");
        }

        var first = Math.Clamp(range.First, 0, rows.Count - 1);
        var last = Math.Clamp(range.Last, first, rows.Count - 1);
        var picked = rows.EntriesOfRows(first, last).Select(index => entries[index]).ToList();

        return Of(picked, section, null);
    }

    private static StagingPlan Of(List<StatusEntry> picked, WorkingTreeSection section, string? folderName)
    {
        var usable = picked.Where(entry => entry.Kind != StatusEntryKind.Unmerged).ToList();
        var skipped = picked.Count - usable.Count;
        if (usable.Count == 0)
        {
            return StagingPlan.Refused(
                $"Only conflicted files are selected ({skipped} skipped; mark one resolved on its own)");
        }

        var name = string.Empty;
        if (folderName is not null)
        {
            name = $"{folderName} ({CountText.Plural(usable.Count, "file")})";
        }
        else if (usable.Count == 1)
        {
            name = PrintableText.Of(usable[0].Path);
        }

        return StagingPlan.For([.. usable.SelectMany(entry => PathsOf(entry, section))], name, usable.Count, skipped);
    }

    private static IReadOnlyList<string> PathsOf(StatusEntry entry, WorkingTreeSection section) =>
        StagingPaths.Of(
            entry,
            section == WorkingTreeSection.Staged ? WorkingDiffSide.Staged : WorkingDiffSide.Unstaged);
}
