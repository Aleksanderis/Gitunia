using Gitunia.Core.Search;
using Gitunia.Git.Refs.Models;

namespace Gitunia.Core.Branches;

/// <summary>
/// Builds the candidate list of the switch picker (<c>&lt;space&gt;bs</c>): local branches, remote branches without a
/// local twin and, from the graph, the selected commit detached.
/// </summary>
public static class SwitchCandidates
{
    private const int ShortHashLength = 7;
    private const char RemoteSeparator = '/';

    /// <param name="refs">The tab's refs.</param>
    /// <param name="commitHash">The graph's selected commit, null when none (or the sidebar has the focus).</param>
    /// <param name="query">What was typed; matched fuzzily against the labels.</param>
    public static IReadOnlyList<SwitchCandidate> Build(IEnumerable<GitRef> refs, string? commitHash, string query)
    {
        var all = refs.Where(reference => reference.SymbolicTarget is null).ToList();
        var localNames = all.Where(reference => reference.Kind == RefKind.LocalBranch)
            .Select(reference => reference.Name)
            .ToHashSet(StringComparer.Ordinal);
        var candidates = new List<SwitchCandidate>();
        foreach (var reference in all)
        {
            if (reference.Kind == RefKind.LocalBranch)
            {
                var elsewhere = reference.IsCurrent ? null : reference.CheckedOutIn;
                candidates.Add(new SwitchCandidate(
                    SwitchTargetKind.LocalBranch,
                    reference.Name,
                    reference.Name,
                    elsewhere,
                    reference.IsCurrent));
            }
            else if (reference.Kind == RefKind.RemoteBranch && !localNames.Contains(LocalNameOf(reference.Name)))
            {
                candidates.Add(
                    new SwitchCandidate(SwitchTargetKind.RemoteBranch, reference.Name, reference.Name, null, false));
            }
        }

        if (commitHash is not null)
        {
            var shortHash = commitHash[..Math.Min(ShortHashLength, commitHash.Length)];
            candidates.Add(new SwitchCandidate(
                SwitchTargetKind.DetachedCommit,
                $"detached at {shortHash}",
                commitHash,
                null,
                false));
        }

        var pattern = query.Trim();

        // OrderByDescending is stable, so equal scores keep the order local, remote, detached.
        return candidates
            .Where(candidate => FuzzyMatcher.Score(pattern, candidate.Label) is not null)
            .OrderByDescending(candidate => FuzzyMatcher.Score(pattern, candidate.Label))
            .ToList();
    }

    /// <summary>
    /// The local name git gives a tracking branch made from <paramref name="remoteBranch"/>: <c>origin/topic</c> becomes
    /// <c>topic</c>.
    /// </summary>
    public static string LocalNameOf(string remoteBranch)
    {
        var slash = remoteBranch.IndexOf(RemoteSeparator, StringComparison.Ordinal);
        return slash < 0 ? remoteBranch : remoteBranch[(slash + 1)..];
    }
}
