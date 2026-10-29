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

    /// <summary>The branch names of the commits that carry more than one (see <see cref="NamesOf"/>), without the first.</summary>
    /// <param name="decorationsByCommit">The refs of the commits that carry one.</param>
    public static IReadOnlyDictionary<ObjectId, IReadOnlyList<string>> ExtraNamesOf(
        IReadOnlyDictionary<ObjectId, IReadOnlyList<CommitDecoration>> decorationsByCommit)
    {
        var extras = new Dictionary<ObjectId, IReadOnlyList<string>>();
        foreach (var (commit, decorations) in decorationsByCommit)
        {
            var names = NamesOf(decorations);
            if (names.Count > 1)
            {
                extras[commit] = names.Skip(1).ToArray();
            }
        }

        return extras;
    }

    /// <summary>
    /// Every distinct branch name of a commit without remote prefix (a local branch and its remote-tracking branch are
    /// one name; <c>origin/HEAD</c> excluded): <see cref="PrimaryName"/> first, then the others in ordinal order.
    /// </summary>
    /// <param name="decorations">The refs on the commit.</param>
    public static IReadOnlyList<string> NamesOf(IReadOnlyList<CommitDecoration> decorations)
    {
        var primary = PrimaryName(decorations);
        if (primary is null)
        {
            return [];
        }

        var others = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var decoration in decorations)
        {
            if (decoration.Kind is not (RefKind.LocalBranch or RefKind.RemoteBranch))
            {
                continue;
            }

            var name = ShortName(decoration.Kind, decoration.Name);
            if (name != SymbolicRemoteHeadName || decoration.Kind == RefKind.LocalBranch)
            {
                others.Add(name);
            }
        }

        others.Remove(primary);
        return [primary, .. others];
    }

    /// <summary>The branch name without the remote name of a remote-tracking branch.</summary>
    /// <param name="kind">Whether the ref is a local or a remote-tracking branch.</param>
    /// <param name="name">The ref name as shown (<c>main</c>, <c>origin/main</c>).</param>
    public static string ShortName(RefKind kind, string name) =>
        kind == RefKind.RemoteBranch ? name[(name.IndexOf('/', StringComparison.Ordinal) + 1)..] : name;

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
