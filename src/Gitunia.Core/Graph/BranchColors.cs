using Gitunia.Git;
using Gitunia.Git.Commits.Models;
using Gitunia.Git.Refs.Models;

namespace Gitunia.Core.Graph;

/// <summary>
/// Which branch a commit belongs to for colouring (#761): every branch name gets one of <see cref="SlotCount"/> colour
/// slots, the same for the local branch and its remote-tracking branch (the remote prefix is ignored). Pure.
/// </summary>
public static class BranchColors
{
    /// <summary>Colour slots of the graph; the palette maps a slot to one of its lane colours.</summary>
    public const int SlotCount = 12;

    private const string SymbolicRemoteHeadName = "HEAD";
    private const uint HashOffset = 2166136261;
    private const uint HashPrime = 16777619;

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
    /// The branch a commit stands for: its first local branch, else its first remote-tracking branch (without the
    /// remote name, the symbolic <c>origin/HEAD</c> excluded); null when it carries no branch ref (tags and a
    /// detached HEAD are not branches).
    /// </summary>
    /// <param name="decorations">The refs on the commit.</param>
    public static string? PrimaryName(IReadOnlyList<CommitDecoration> decorations)
    {
        string? remote = null;
        foreach (var decoration in decorations)
        {
            if (decoration.Kind == RefKind.LocalBranch)
            {
                return decoration.Name;
            }

            if (remote is null && decoration.Kind == RefKind.RemoteBranch)
            {
                var name = decoration.Name[(decoration.Name.IndexOf('/', StringComparison.Ordinal) + 1)..];
                remote = name == SymbolicRemoteHeadName ? null : name;
            }
        }

        return remote;
    }

    /// <summary>The colour slot a branch name starts from: a stable hash of the name, never of the position.</summary>
    /// <param name="name">The branch name without a remote prefix.</param>
    public static int PreferredSlot(string name)
    {
        var hash = HashOffset;
        foreach (var character in name)
        {
            hash = (hash ^ character) * HashPrime;
        }

        return (int)(hash % SlotCount);
    }
}
