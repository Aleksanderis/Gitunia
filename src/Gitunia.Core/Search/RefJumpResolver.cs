using System.Collections.Immutable;
using Gitunia.Core.Editing;
using Gitunia.Core.Filtering;
using Gitunia.Core.Graph;
using Gitunia.Core.Search.Models;
using Gitunia.Core.Text;
using Gitunia.Git;
using Gitunia.Git.Refs.Models;

namespace Gitunia.Core.Search;

/// <summary>Builds the candidate list of the ref jump prompt (<c>&lt;space&gt;f</c>): refs ranked fuzzily, plus loaded commits whose hash starts with the text.</summary>
public static class RefJumpResolver
{
    /// <summary>The subject of the no-match text.</summary>
    public const string Subject = "ref or commit";

    private const int MinimumHashLength = 4;
    private const int MaximumHashCandidates = 5;

    /// <param name="text">What was typed and its caret.</param>
    /// <param name="refs">The tab's refs.</param>
    /// <param name="graph">The loaded commits, searched for a hash prefix.</param>
    public static RefJumpState Resolve(TextField text, IEnumerable<GitRef> refs, CommitGraph graph)
    {
        var pattern = text.Text.Trim();
        var leading = ImmutableArray<RefJumpCandidate>.Empty;
        if (pattern.Length >= MinimumHashLength && pattern.All(char.IsAsciiHexDigit))
        {
            leading = [.. FindCommits(pattern, graph)];
        }

        var items = ItemsOf(refs);
        var list = FilterLists.Of(items, MatcherKind.Fuzzy, UiTexts.NoMatch(Subject), text);

        return new RefJumpState(list.WithLeading(leading));
    }

    // Equal scores keep this order (the filter list is stable): local branches, remote branches, the rest, by name.
    private static ImmutableArray<FilterItem<RefJumpCandidate>> ItemsOf(IEnumerable<GitRef> refs)
    {
        var items = new List<RankedItem>();
        foreach (var reference in refs)
        {
            if (reference.SymbolicTarget is not null
                || reference.Kind is RefKind.Head or RefKind.Other
                || !ObjectId.TryParse(reference.CommitHash, out var commitId))
            {
                continue;
            }

            var candidate = new RefJumpCandidate(reference.Name, commitId, reference.Kind);
            var item = FilterItems.Of(candidate, reference.Name, reference.IsCurrent ? 1 : 0);
            items.Add(new RankedItem(item, KindOrder(reference.Kind)));
        }

        return
        [
            .. items
                .OrderBy(ranked => ranked.Order)
                .ThenBy(ranked => ranked.Item.Value.Label, StringComparer.Ordinal)
                .Select(ranked => ranked.Item),
        ];
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
            if (entry.Id.StartsWith(prefix))
            {
                found.Add(new RefJumpCandidate($"{ShortHash.Of(entry.Id.ToString())} {entry.Subject}", entry.Id, null));
            }
        }

        return found;
    }

    private readonly record struct RankedItem(FilterItem<RefJumpCandidate> Item, int Order);
}
