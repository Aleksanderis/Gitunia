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
    /// <param name="onlyBranches">
    /// When set, only the branches with these labels are listed (the detached entry stays); null lists all.
    /// </param>
    public static IReadOnlyList<SwitchCandidate> Build(
        IEnumerable<GitRef> refs,
        string? commitHash,
        string query,
        IReadOnlySet<string>? onlyBranches = null)
    {
        var all = refs.Where(reference => reference.SymbolicTarget is null).ToList();
        var localNames = all.Where(reference => reference.Kind == RefKind.LocalBranch)
            .Select(reference => reference.Name)
            .ToHashSet(StringComparer.Ordinal);
        var candidates = new List<SwitchCandidate>();
        foreach (var reference in all)
        {
            if (onlyBranches is not null && !IsWanted(onlyBranches, LabelOf(reference, localNames)))
            {
                continue;
            }

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
    /// The labels of the branches a checkout can pick at <paramref name="commitHash"/>: the local branches there and
    /// the remote branches there that have no local twin.
    /// </summary>
    public static IReadOnlySet<string> BranchesAt(IEnumerable<GitRef> refs, string commitHash)
    {
        var all = refs.Where(reference => reference.SymbolicTarget is null).ToList();
        var localNames = all.Where(reference => reference.Kind == RefKind.LocalBranch)
            .Select(reference => reference.Name)
            .ToHashSet(StringComparer.Ordinal);
        return all
            .Where(reference => reference.CommitHash == commitHash)
            .Select(reference => LabelOf(reference, localNames))
            .OfType<string>()
            .ToHashSet(StringComparer.Ordinal);
    }

    /// <summary>The remote branches (not <c>origin/HEAD</c>) matching <paramref name="query"/>, best first.</summary>
    public static IReadOnlyList<SwitchCandidate> BuildRemote(IEnumerable<GitRef> refs, string query)
    {
        var pattern = query.Trim();
        return refs
            .Where(reference => reference.Kind == RefKind.RemoteBranch && reference.SymbolicTarget is null)
            .Select(reference =>
                new SwitchCandidate(SwitchTargetKind.RemoteBranch, reference.Name, reference.Name, null, false))
            .Where(candidate => FuzzyMatcher.Score(pattern, candidate.Label) is not null)
            .OrderByDescending(candidate => FuzzyMatcher.Score(pattern, candidate.Label))
            .ToList();
    }

    /// <summary>
    /// The local name git gives a tracking branch made from <paramref name="remoteBranch"/>:
    /// <c>origin/topic</c> becomes <c>topic</c>.
    /// </summary>
    public static string LocalNameOf(string remoteBranch)
    {
        var slash = remoteBranch.IndexOf(RemoteSeparator, StringComparison.Ordinal);
        return slash < 0 ? remoteBranch : remoteBranch[(slash + 1)..];
    }

    private static bool IsWanted(IReadOnlySet<string> only, string? label) => label is not null && only.Contains(label);

    // The label a branch has in the list, or null for a ref that is not listed (a remote branch with a local twin).
    private static string? LabelOf(GitRef reference, HashSet<string> localNames) => reference.Kind switch
    {
        RefKind.LocalBranch => reference.Name,
        RefKind.RemoteBranch when !localNames.Contains(LocalNameOf(reference.Name)) => reference.Name,
        _ => null,
    };
}
