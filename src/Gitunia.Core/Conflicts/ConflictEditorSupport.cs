using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Git.Status.Models;

namespace Gitunia.Core.Conflicts;

/// <summary>
/// Which conflicts the built-in conflict editor takes, in one place so the keys, hints, banner and chips never
/// advertise the editor for a file it refuses (#742). Both-modified files (UU) and both-added files (AA, add/add:
/// two sides against an empty base) open; delete/modify and the other kinds go to the mergetool
/// (<c>&lt;space&gt;tm</c>). The reader can still refuse a particular file (binary, non-UTF-8, too large, markers
/// gone) after its kind passed.
/// </summary>
public static class ConflictEditorSupport
{
    /// <summary>Whether the editor takes a conflict of this kind.</summary>
    public static bool CanOpen(ConflictKind kind) => kind is ConflictKind.BothModified or ConflictKind.BothAdded;

    /// <summary>Whether any conflicted file of the working tree is one the editor takes.</summary>
    /// <param name="tree">The working tree of the tab, null while the status is not loaded.</param>
    public static bool AnyCanOpen(WorkingTreeState? tree) =>
        tree?.Snapshot.Entries.Any(entry => entry.Kind == StatusEntryKind.Unmerged && CanOpen(entry.Conflict)) ?? false;

    /// <summary>What the status line says about a kind the editor refuses.</summary>
    public static string Refusal(ConflictKind kind) => kind switch
    {
        ConflictKind.BothDeleted => "both sides deleted it",
        ConflictKind.AddedByUs => "only we added it",
        ConflictKind.AddedByThem => "only they added it",
        ConflictKind.DeletedByUs => "we deleted it (delete/modify)",
        ConflictKind.DeletedByThem => "they deleted it (delete/modify)",
        _ => "this conflict cannot be resolved hunk by hunk",
    };
}
