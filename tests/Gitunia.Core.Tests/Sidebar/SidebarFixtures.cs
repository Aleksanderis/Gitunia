using System.Collections.Immutable;
using Gitunia.Core.Workspaces;
using Gitunia.Git.Refs.Models;
using Gitunia.Git.Repositories.Models;
using Gitunia.Git.Stash;
using Gitunia.Git.Worktrees;

namespace Gitunia.Core.Tests.Sidebar;

/// <summary>Builds tabs with refs, worktrees and stashes for the sidebar tests.</summary>
internal static class SidebarFixtures
{
    public const string RepositoryPath = "/work/repo";

    public static RepoState Tab(
        IEnumerable<GitRef>? refs = null,
        IEnumerable<WorktreeInfo>? worktrees = null,
        IEnumerable<StashEntry>? stashes = null)
    {
        var location = new RepositoryLocation(RepositoryPath, Path.Combine(RepositoryPath, ".git"), Path.Combine(RepositoryPath, ".git"), false, false);
        var tab = RepoState.Create(new TabId(1), location);
        return tab with
        {
            Refs = [.. refs ?? []],
            Worktrees = [.. worktrees ?? []],
            Stashes = [.. stashes ?? []],
        };
    }

    public static GitRef Branch(string name, string? checkedOutIn = null, bool isCurrent = false, int? ahead = null, int? behind = null) =>
        Ref("refs/heads/" + name, name, RefKind.LocalBranch, checkedOutIn, isCurrent, ahead, behind, null);

    public static GitRef Remote(string name, string? symbolicTarget = null) =>
        Ref("refs/remotes/" + name, name, RefKind.RemoteBranch, null, false, null, null, symbolicTarget);

    public static GitRef Tag(string name) => Ref("refs/tags/" + name, name, RefKind.Tag, null, false, null, null, null);

    public static WorktreeInfo Worktree(string path, string? branch, bool isMain = false, bool isLocked = false, bool isPrunable = false) =>
        new(path, new string('a', 40), branch is null ? null : "refs/heads/" + branch, branch is null, false, isLocked, null, isPrunable, null, isMain);

    public static StashEntry Stash(int index, string message) => new(index, $"stash@{{{index}}}", new string('c', 40), message);

    private static GitRef Ref(string fullName, string name, RefKind kind, string? checkedOutIn, bool isCurrent, int? ahead, int? behind, string? symbolicTarget) =>
        new(fullName, name, kind, new string('b', 40), new string('b', 40), null, ahead, behind, false, isCurrent, checkedOutIn, symbolicTarget);
}
