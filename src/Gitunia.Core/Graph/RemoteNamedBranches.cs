using System.Collections.Immutable;
using Gitunia.Git.Refs.Models;

namespace Gitunia.Core.Graph;

/// <summary>
/// The local branches whose graph label names its remotes (pure, #1495): the branch has remote-tracking branches on
/// more than one remote and they do not all point at the same commit, so the bare remote glyph would not say which
/// remote a label stands for.
/// </summary>
public static class RemoteNamedBranches
{
    private const string SymbolicRemoteHeadName = "HEAD";

    /// <summary>The short names of the local branches that need their remotes spelled out.</summary>
    public static IReadOnlySet<string> Of(IEnumerable<GitRef> refs)
    {
        var commitsByBranch = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);
        var locals = new HashSet<string>(StringComparer.Ordinal);
        foreach (var reference in refs)
        {
            if (reference.Kind == RefKind.LocalBranch)
            {
                locals.Add(reference.Name);
            }
            else if (reference.Kind == RefKind.RemoteBranch)
            {
                Add(commitsByBranch, reference);
            }
        }

        return commitsByBranch
            .Where(entry => locals.Contains(entry.Key) && IsAmbiguous(entry.Value))
            .Select(entry => entry.Key)
            .ToImmutableHashSet(StringComparer.Ordinal);
    }

    // Remote names rarely contain a slash, so the first segment is the remote (as in RefLabelBuilder).
    private static void Add(Dictionary<string, Dictionary<string, string>> commitsByBranch, GitRef remote)
    {
        var slash = remote.Name.IndexOf('/', StringComparison.Ordinal);
        if (slash < 0)
        {
            return;
        }

        var branch = remote.Name[(slash + 1)..];
        if (branch == SymbolicRemoteHeadName)
        {
            return;
        }

        if (!commitsByBranch.TryGetValue(branch, out var commits))
        {
            commits = new Dictionary<string, string>(StringComparer.Ordinal);
            commitsByBranch[branch] = commits;
        }

        commits[remote.Name[..slash]] = remote.CommitHash;
    }

    private static bool IsAmbiguous(Dictionary<string, string> commitByRemote) =>
        commitByRemote.Count > 1 && commitByRemote.Values.Distinct(StringComparer.Ordinal).Count() > 1;
}
