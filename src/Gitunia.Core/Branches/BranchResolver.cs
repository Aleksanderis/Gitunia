using System.Collections.Immutable;
using Gitunia.Core.Sidebar;
using Gitunia.Git.Refs;
using Gitunia.Git.Refs.Models;

namespace Gitunia.Core.Branches;

/// <summary>Works out which branch a selection names, from the tab's refs only (no git).</summary>
public static class BranchResolver
{
    private const string SelectNotice = "Select a local branch (sidebar) or a commit with one branch on it";

    /// <summary>Finds the local branch a rename, upstream change or fast-forward acts on, or says why none.</summary>
    public static string? ResolveLocal(BranchTarget target, ImmutableArray<GitRef> refs, out GitRef? branch)
    {
        branch = null;
        var locals = refs.Where(reference => reference.Kind == RefKind.LocalBranch).ToList();
        if (target.RowKind is not null)
        {
            if (target.RowKind == SidebarRowKind.LocalBranch)
            {
                branch = locals.FirstOrDefault(reference => reference.Name == target.Name);
            }

            return branch is null ? "Select a local branch first" : null;
        }

        var here = new List<GitRef>();
        if (target.CommitHash is not null)
        {
            here = [.. locals.Where(reference => reference.CommitHash == target.CommitHash)];
        }

        if (here.Count > 1)
        {
            return $"{here.Count} branches point here: pick one in the sidebar";
        }

        branch = here.FirstOrDefault();

        return branch is null ? SelectNotice : null;
    }

    /// <summary>The local branches a folder row holds, or those on the selected graph commit.</summary>
    public static List<string> DeleteChoices(BranchTarget target, ImmutableArray<GitRef> refs)
    {
        var locals = refs.Where(reference => reference.Kind == RefKind.LocalBranch);
        if (target.RowKind == SidebarRowKind.Folder)
        {
            var prefix = $"{target.Name}/";

            return [.. locals.Where(reference => reference.Name.StartsWith(prefix, StringComparison.Ordinal))
                .Select(reference => reference.Name)];
        }

        if (target.RowKind is null && target.CommitHash is { } hash)
        {
            return [.. locals.Where(reference => reference.CommitHash == hash).Select(reference => reference.Name)];
        }

        return [];
    }

    /// <summary>The label of the one branch the cursor names: a sidebar row, or the only one on the commit.</summary>
    public static string? BranchUnderCursor(BranchTarget target, ImmutableArray<GitRef> refs)
    {
        if (target.RowKind is SidebarRowKind.LocalBranch or SidebarRowKind.RemoteBranch && target.Name is { } name)
        {
            var hasTwin = refs.Any(reference =>
                reference.Kind == RefKind.LocalBranch && reference.Name == SwitchCandidates.LocalNameOf(name));

            return target.RowKind == SidebarRowKind.RemoteBranch && hasTwin ? SwitchCandidates.LocalNameOf(name) : name;
        }

        if (target.RowKind is null && target.CommitHash is { } hash)
        {
            var here = SwitchCandidates.BranchesAt(refs, hash);

            return here.Count == 1 ? here.First() : null;
        }

        return null;
    }

    /// <summary>The commit a ref or a hash names, for the graph cursor to land on; null when it is not known.</summary>
    public static string? CommitOf(ImmutableArray<GitRef> refs, string refOrHash)
    {
        var reference = refs.FirstOrDefault(candidate => candidate.FullName == refOrHash);

        return reference?.CommitHash ?? refOrHash;
    }

    /// <summary>The commit HEAD is on (the branch checked out, read from the refs), null when none.</summary>
    public static string? HeadCommit(ImmutableArray<GitRef> refs) =>
        refs.FirstOrDefault(item => item is { Kind: RefKind.LocalBranch, IsCurrent: true })?.CommitHash;

    /// <summary>True when a local branch is called <paramref name="name"/>.</summary>
    public static bool HasLocal(ImmutableArray<GitRef> refs, string name) =>
        refs.Any(reference => reference.Kind == RefKind.LocalBranch && reference.Name == name);

    /// <summary>The upstream as shown to the user, without its <c>refs/remotes/</c> or <c>refs/heads/</c>.</summary>
    public static string ShownUpstream(string upstream)
    {
        if (upstream.StartsWith(GitRefPrefixes.Remotes, StringComparison.Ordinal))
        {
            return upstream[GitRefPrefixes.Remotes.Length..];
        }

        if (upstream.StartsWith(GitRefPrefixes.Heads, StringComparison.Ordinal))
        {
            return upstream[GitRefPrefixes.Heads.Length..];
        }

        return upstream;
    }
}
