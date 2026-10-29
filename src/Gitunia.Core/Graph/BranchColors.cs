using Gitunia.Git;
using Gitunia.Git.Commits.Models;
using Gitunia.Git.Refs.Models;

namespace Gitunia.Core.Graph;

/// <summary>
/// Which branch a commit belongs to for colouring (#761): every branch name gets one of the palette's colours (at
/// most <see cref="SlotCount"/>), the same for the local branch and its remote-tracking branch (the remote prefix is
/// ignored). Pure.
/// </summary>
public static class BranchColors
{
    /// <summary>Colour slots of the graph; the palette maps a slot to one of its lane colours.</summary>
    public const int SlotCount = 12;

    private const string SymbolicRemoteHeadName = "HEAD";
    private const uint HashOffset = 2166136261;
    private const uint HashPrime = 16777619;

    private static readonly string[] TrunkNames = ["main", "master", "trunk"];

    /// <summary>The branch name of every commit that carries a branch ref, see <see cref="PrimaryName"/>.</summary>
    /// <param name="decorationsByCommit">The refs of the commits that carry one.</param>
    public static IReadOnlyDictionary<ObjectId, string> TipsOf(
        IReadOnlyDictionary<ObjectId, IReadOnlyList<CommitDecoration>> decorationsByCommit)
    {
        var tips = new Dictionary<ObjectId, string>();
        foreach (var (commit, decorations) in decorationsByCommit)
        {
            if (PrimaryName(decorations) is { } name)
            {
                tips[commit] = name;
            }
        }

        return tips;
    }

    /// <summary>
    /// The branch a commit stands for: its checked-out local branch, else its smallest local branch name (ordinal),
    /// else its smallest remote-tracking branch name without the remote name (the symbolic <c>origin/HEAD</c>
    /// excluded); null when it carries no branch ref (tags and a detached HEAD are not branches). Independent of the
    /// order git lists the refs in.
    /// </summary>
    /// <param name="decorations">The refs on the commit.</param>
    public static string? PrimaryName(IReadOnlyList<CommitDecoration> decorations)
    {
        string? local = null;
        string? remote = null;
        foreach (var decoration in decorations)
        {
            if (decoration.Kind == RefKind.LocalBranch)
            {
                if (decoration.IsCurrent)
                {
                    return decoration.Name;
                }

                local = Smaller(local, decoration.Name);
            }
            else if (decoration.Kind == RefKind.RemoteBranch)
            {
                var name = decoration.Name[(decoration.Name.IndexOf('/', StringComparison.Ordinal) + 1)..];
                remote = name == SymbolicRemoteHeadName ? remote : Smaller(remote, name);
            }
        }

        return local ?? remote;
    }

    /// <summary>
    /// The branch whose line the shared history below a fork belongs to: <c>main</c>, <c>master</c> or <c>trunk</c>
    /// when a branch of that name exists (in this order; local or remote, on any commit), else the checked-out
    /// branch, else none. Where several lanes meet at a commit, the lane of this branch keeps it, so the main line
    /// stays straight whatever order the tips of the other branches come in.
    /// </summary>
    /// <param name="decorationsByCommit">The refs of the commits that carry one.</param>
    public static string? TrunkOf(IReadOnlyDictionary<ObjectId, IReadOnlyList<CommitDecoration>> decorationsByCommit)
    {
        foreach (var candidate in TrunkNames)
        {
            foreach (var decorations in decorationsByCommit.Values)
            {
                if (HasBranch(decorations, candidate))
                {
                    return candidate;
                }
            }
        }

        return CheckedOutBranchOf(decorationsByCommit);
    }

    /// <summary>The commits that carry a branch of the trunk's name (local or remote), and whatever else.</summary>
    /// <param name="decorationsByCommit">The refs of the commits that carry one.</param>
    /// <param name="trunk">The trunk branch, see <see cref="TrunkOf"/>; null for none.</param>
    public static IReadOnlySet<ObjectId> TrunkCommitsOf(
        IReadOnlyDictionary<ObjectId, IReadOnlyList<CommitDecoration>> decorationsByCommit,
        string? trunk)
    {
        var commits = new HashSet<ObjectId>();
        if (trunk is null)
        {
            return commits;
        }

        foreach (var (commit, decorations) in decorationsByCommit)
        {
            if (HasBranch(decorations, trunk))
            {
                commits.Add(commit);
            }
        }

        return commits;
    }

    /// <summary>
    /// Whether two ref lists of one commit have the same branches (remote prefix ignored) and the same primary name:
    /// what the layout of the rows depends on. Tags and the order of the refs do not matter.
    /// </summary>
    /// <param name="first">The refs of the commit.</param>
    /// <param name="second">The refs of the same commit at another time.</param>
    public static bool HaveSameBranches(IReadOnlyList<CommitDecoration> first, IReadOnlyList<CommitDecoration> second)
    {
        foreach (var decoration in first)
        {
            if (BranchNameOf(decoration) is { } name && !HasBranch(second, name))
            {
                return false;
            }
        }

        foreach (var decoration in second)
        {
            if (BranchNameOf(decoration) is { } name && !HasBranch(first, name))
            {
                return false;
            }
        }

        return PrimaryName(first) == PrimaryName(second);
    }

    /// <summary>The checked-out local branch among the refs, or null (detached HEAD, unborn branch).</summary>
    /// <param name="decorationsByCommit">The refs of the commits that carry one.</param>
    public static string? CheckedOutBranchOf(
        IReadOnlyDictionary<ObjectId, IReadOnlyList<CommitDecoration>> decorationsByCommit)
    {
        foreach (var decorations in decorationsByCommit.Values)
        {
            foreach (var decoration in decorations)
            {
                if (decoration is { IsCurrent: true, Kind: RefKind.LocalBranch })
                {
                    return decoration.Name;
                }
            }
        }

        return null;
    }

    private static bool HasBranch(IReadOnlyList<CommitDecoration> decorations, string name)
    {
        foreach (var decoration in decorations)
        {
            if (BranchNameOf(decoration) == name)
            {
                return true;
            }
        }

        return false;
    }

    // The branch a ref stands for: a local branch's name, a remote branch's without its remote; null for the rest.
    private static string? BranchNameOf(CommitDecoration decoration)
    {
        if (decoration.Kind == RefKind.LocalBranch)
        {
            return decoration.Name;
        }

        if (decoration.Kind != RefKind.RemoteBranch)
        {
            return null;
        }

        var name = decoration.Name[(decoration.Name.IndexOf('/', StringComparison.Ordinal) + 1)..];

        return name == SymbolicRemoteHeadName ? null : name;
    }

    /// <summary>The colour slot a branch name starts from: a stable hash of the name, never of the position.</summary>
    /// <param name="name">The branch name without a remote prefix.</param>
    /// <param name="slotCount">Distinct colours the palette has.</param>
    public static int PreferredSlot(string name, int slotCount = SlotCount) => (int)(HashOf(name) % (uint)slotCount);

    /// <summary>A stable 32-bit hash of a branch name (FNV-1a over its UTF-16 characters).</summary>
    /// <param name="name">The branch name without a remote prefix.</param>
    public static uint HashOf(string name)
    {
        var hash = HashOffset;
        foreach (var character in name)
        {
            hash = (hash ^ character) * HashPrime;
        }

        return hash;
    }

    private static string Smaller(string? current, string candidate) =>
        current is null || string.CompareOrdinal(candidate, current) < 0 ? candidate : current;
}
