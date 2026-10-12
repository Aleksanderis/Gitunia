using Gitunia.Core.Graph;
using Gitunia.Git;

namespace Gitunia.Core.Search;

/// <summary>Builds the candidate list of the <c>gr</c> prompt: refs ranked fuzzily, plus loaded commits whose hash starts with the text.</summary>
public static class RefJumpResolver
{
    private const int MinimumHashLength = 4;
    private const int MaximumHashCandidates = 5;

    /// <param name="text">What was typed.</param>
    /// <param name="refs">The tab's refs.</param>
    /// <param name="graph">The loaded commits, searched for a hash prefix.</param>
    public static RefJumpState Resolve(string text, IEnumerable<GitRef> refs, CommitGraph graph)
    {
        var pattern = text.Trim();
        var candidates = new List<RefJumpCandidate>();
        if (pattern.Length >= MinimumHashLength && pattern.All(char.IsAsciiHexDigit))
        {
            candidates.AddRange(FindCommits(pattern, graph));
        }

        var ranked = new List<(RefJumpCandidate Candidate, int Score, int Order)>();
        foreach (var reference in refs)
        {
            if (reference.SymbolicTarget is not null || reference.Kind is RefKind.Head or RefKind.Other || !ObjectId.TryParse(reference.CommitHash, out var commitId))
            {
                continue;
            }

            if (FuzzyMatcher.Score(pattern, reference.Name) is { } score)
            {
                ranked.Add((new RefJumpCandidate(reference.Name, commitId, reference.Kind), score + (reference.IsCurrent ? 1 : 0), KindOrder(reference.Kind)));
            }
        }

        candidates.AddRange(ranked
            .OrderByDescending(item => item.Score)
            .ThenBy(item => item.Order)
            .ThenBy(item => item.Candidate.Label, StringComparer.Ordinal)
            .Select(item => item.Candidate));
        return new RefJumpState(text, [.. candidates], 0);
    }

    private static int KindOrder(RefKind kind) => kind switch
    {
        RefKind.LocalBranch => 0,
        RefKind.RemoteBranch => 1,
        _ => 2,
    };

    private static List<RefJumpCandidate> FindCommits(string prefix, CommitGraph graph)
    {
        var found = new List<RefJumpCandidate>();
        for (var index = 0; index < graph.Count && found.Count < MaximumHashCandidates; index++)
        {
            var entry = graph[index];
            var hash = entry.Id.ToString();
            if (hash.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                found.Add(new RefJumpCandidate(hash[..Math.Min(hash.Length, 7)] + " " + entry.Subject, entry.Id, null));
            }
        }

        return found;
    }
}
