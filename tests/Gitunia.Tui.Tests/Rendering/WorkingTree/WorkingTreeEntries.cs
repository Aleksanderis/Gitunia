using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Git.Commits.Models;
using Gitunia.Git.Refs.Models;
using Gitunia.Git.Status.Models;

namespace Gitunia.Tui.Tests.Rendering.WorkingTree;

/// <summary>Builds status entries and working-tree states for tests.</summary>
public static class WorkingTreeEntries
{
    public static StatusEntry Modified(string path) =>
        new(StatusEntryKind.Ordinary, path, null, FileChangeKind.Unmodified, FileChangeKind.Modified, null, ConflictKind.None, null);

    public static StatusEntry Deleted(string path) => Modified(path) with { WorktreeChange = FileChangeKind.Deleted };

    public static StatusEntry StagedAdded(string path) =>
        Modified(path) with { IndexChange = FileChangeKind.Added, WorktreeChange = FileChangeKind.Unmodified };

    public static StatusEntry StagedModified(string path) =>
        Modified(path) with { IndexChange = FileChangeKind.Modified, WorktreeChange = FileChangeKind.Unmodified };

    public static StatusEntry StagedRename(string from, string to) =>
        new(StatusEntryKind.Renamed, to, from, FileChangeKind.Renamed, FileChangeKind.Unmodified, 100, ConflictKind.None, null);

    public static StatusEntry Untracked(string path) =>
        new(StatusEntryKind.Untracked, path, null, FileChangeKind.Unmodified, FileChangeKind.Unmodified, null, ConflictKind.None, null);

    public static StatusEntry Conflict(string path, ConflictKind kind = ConflictKind.BothModified) =>
        new(StatusEntryKind.Unmerged, path, null, FileChangeKind.UpdatedButUnmerged, FileChangeKind.UpdatedButUnmerged, null, kind, null);

    public static StatusSnapshot Snapshot(params StatusEntry[] entries) =>
        new(new StatusBranch(new HeadState(HeadKind.Branch, "main", null), null, null, null), entries, []);

    public static WorkingTreeState Tree(params StatusEntry[] entries) => WorkingTreeState.Reconcile(null, Snapshot(entries));
}
