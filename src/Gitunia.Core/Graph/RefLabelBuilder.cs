using Gitunia.Git;

namespace Gitunia.Core.Graph;

/// <summary>Turns the refs on one commit into the labels the graph shows (pure).</summary>
public static class RefLabelBuilder
{
    private const string SymbolicRemoteHeadName = "HEAD";

    /// <summary>
    /// Hides the symbolic remote HEAD (<c>origin/HEAD</c>) and folds a remote-tracking branch into the local branch of the same
    /// name on the same commit. A remote branch without such a local branch stays separate, as does one on another commit
    /// (it is not in the list of this commit at all). Order of the input is kept.
    /// </summary>
    public static IReadOnlyList<RefLabel> Build(IReadOnlyList<CommitDecoration> decorations)
    {
        if (decorations.Count == 0)
        {
            return [];
        }

        var labels = new List<RefLabel>(decorations.Count);
        foreach (var decoration in decorations)
        {
            if (decoration.Kind == RefKind.RemoteBranch)
            {
                if (IsSymbolicRemoteHead(decoration) || HasLocalBranch(decorations, decoration))
                {
                    continue;
                }
            }

            var hasRemote = decoration.Kind == RefKind.LocalBranch && HasRemoteBranch(decorations, decoration);
            labels.Add(new RefLabel(decoration.Name, decoration.Kind, decoration.IsCurrent, hasRemote));
        }

        return labels;
    }

    // Remote names rarely contain a slash, so the first segment is taken as the remote and the rest as the branch.
    private static bool IsSymbolicRemoteHead(CommitDecoration decoration) =>
        BranchPart(decoration.Name) == SymbolicRemoteHeadName;

    private static bool HasLocalBranch(IReadOnlyList<CommitDecoration> decorations, CommitDecoration remote) =>
        decorations.Any(other => other.Kind == RefKind.LocalBranch && IsRemoteOf(remote, other));

    private static bool HasRemoteBranch(IReadOnlyList<CommitDecoration> decorations, CommitDecoration local) =>
        decorations.Any(other => other.Kind == RefKind.RemoteBranch && !IsSymbolicRemoteHead(other) && IsRemoteOf(other, local));

    private static bool IsRemoteOf(CommitDecoration remote, CommitDecoration local) =>
        BranchPart(remote.Name) == local.Name;

    private static string BranchPart(string remoteBranchName) =>
        remoteBranchName[(remoteBranchName.IndexOf('/', StringComparison.Ordinal) + 1)..];
}
