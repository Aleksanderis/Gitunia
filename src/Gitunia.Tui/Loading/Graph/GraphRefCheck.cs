using Gitunia.Core.Graph;
using Gitunia.Git.Refs;
using Gitunia.Git.Refs.Models;
using Gitunia.Git.Repositories.Models;

namespace Gitunia.Tui.Loading.Graph;

/// <summary>
/// Tells whether a loaded graph still shows the refs and HEAD of a snapshot read later. The refresher's baseline pass uses it to
/// catch a change made between the initial graph load and the baseline read, which the watcher event can no longer reveal (the
/// baseline snapshot already contains it, so the next refresh diffs equal). The current-branch marker counts too. A ref that can
/// never be decorated in the log (a tag on a non-commit object) makes the check report a difference once per baseline, which costs
/// one incremental reload and cannot loop.
/// </summary>
public static class GraphRefCheck
{
    private const string CurrentKey = "current branch";

    /// <summary>True when a branch, tag or detached HEAD in the snapshot is not decorated the same way in <paramref name="rows"/>, or the other way round.</summary>
    public static bool Differs(IReadOnlyList<GraphEntry> rows, RepositorySnapshot snapshot)
    {
        var shown = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var row in rows)
        {
            foreach (var decoration in row.Decorations)
            {
                if (IsComparable(decoration.FullName, decoration.Kind))
                {
                    shown[decoration.FullName] = row.Id.ToString();
                    if (decoration.IsCurrent && decoration.Kind == RefKind.LocalBranch)
                    {
                        shown[CurrentKey] = decoration.FullName;
                    }
                }
            }
        }

        var expected = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var reference in snapshot.Refs)
        {
            if (IsComparable(reference.FullName, reference.Kind))
            {
                expected[reference.FullName] = reference.CommitHash;
            }
        }

        if (snapshot.Head.Kind == HeadKind.Branch && snapshot.Head.BranchName is not null)
        {
            expected[CurrentKey] = $"{GitRefPrefixes.Heads}{snapshot.Head.BranchName}";
        }

        if (snapshot.Head.Kind == HeadKind.Detached && snapshot.Head.CommitHash is not null)
        {
            expected["HEAD"] = snapshot.Head.CommitHash;
        }

        if (shown.Count != expected.Count)
        {
            return true;
        }

        foreach (var pair in expected)
        {
            if (!shown.TryGetValue(pair.Key, out var hash) || !string.Equals(hash, pair.Value, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    // Symbolic refs such as origin/HEAD are decorated inconsistently and always follow another ref; other kinds (stash) are not in snapshots.
    private static bool IsComparable(string fullName, RefKind kind) =>
        kind is RefKind.LocalBranch or RefKind.RemoteBranch or RefKind.Tag or RefKind.Head
        && !fullName.EndsWith("/HEAD", StringComparison.Ordinal);
}
