using System.Collections.Immutable;
using Gitunia.Core.Effects;
using Gitunia.Core.Sidebar;
using Gitunia.Core.Status;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Core.Worktrees;
using Gitunia.Git.Refs.Models;
using Gitunia.Git.Repositories.Models;
using Gitunia.Git.Worktrees;
using Xunit;

namespace Gitunia.Core.Tests.Worktrees;

/// <summary>Tabs, worktrees, contexts and assertions shared by the worktree reducer tests.</summary>
public static class WorktreeTestData
{
    public const string Main = "1111111111111111111111111111111111111111";
    public const string Topic = "2222222222222222222222222222222222222222";

    public static readonly TabId Tab = new(1);

    public static string Root { get; } = Path.Combine(Path.GetTempPath(), "gitunia-worktree-tests", "repo");

    public static string Sibling(string name) => Path.Combine(Path.GetDirectoryName(Root)!, name);

    public static WorktreeInfo Info(
        string path,
        string? branch = null,
        bool main = false,
        bool locked = false,
        bool prunable = false,
        bool bare = false) => new(
            path,
            Topic,
            branch is null ? null : $"refs/heads/{branch}",
            branch is null,
            bare,
            locked,
            null,
            prunable,
            null,
            main);

    public static GitRef Local(string name, bool current = false, string? checkedOutIn = null) => new(
        $"refs/heads/{name}",
        name,
        RefKind.LocalBranch,
        Main,
        Main,
        null,
        null,
        null,
        false,
        current,
        checkedOutIn,
        null);

    public static RepositoryLocation Location(string root)
    {
        var gitDirectory = Path.Combine(root, ".git");

        return new RepositoryLocation(root, gitDirectory, gitDirectory, false, false);
    }

    public static RepositoryState TabOf(
        TabId id,
        string root,
        ImmutableArray<WorktreeInfo> worktrees = default,
        ImmutableArray<GitRef> refs = default,
        HeadState? head = null) => RepositoryState.Create(id, Location(root)) with
        {
            Worktrees = worktrees.IsDefault ? [] : worktrees,
            Refs = refs.IsDefault ? [] : refs,
            Head = head,
        };

    public static WorktreeContext Context(
        ImmutableArray<WorktreeInfo> worktrees = default,
        ImmutableArray<GitRef> refs = default,
        bool busy = false,
        HeadState? head = null,
        params RepositoryState[] others)
    {
        var tab = TabOf(Tab, Root, worktrees, refs, head);

        return new WorktreeContext(tab, [tab, .. others], busy);
    }

    public static WorktreeTarget Row(SidebarRowKind kind, string label, string? path = null, string? commit = null) =>
        new(kind, label, path, commit, null);

    public static WorktreeTarget WorktreeRow(string path) => Row(SidebarRowKind.Worktree, Path.GetFileName(path), path);

    public static ImmutableArray<WorktreeInfo> MainAnd(params WorktreeInfo[] others) =>
        [Info(Root, "main", main: true), .. others];

    public static WorktreeUpdate Reduce(WorktreeCommand command, WorktreeContext context) =>
        WorktreeReducer.Reduce(command, context);

    public static string Warning(ImmutableArray<AppEffect> effects)
    {
        var post = Assert.Single(effects.OfType<PostStatusEffect>());
        Assert.Equal(StatusSeverity.Warning, post.Severity);
        Assert.Single(effects);

        return post.Text;
    }
}
