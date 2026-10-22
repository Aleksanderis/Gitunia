using System.Collections.Immutable;
using Gitunia.Git.Refs.Models;

namespace Gitunia.Core.Graph;

/// <summary>The local branches whose upstream no longer exists (pure), the ones a prune could clean up.</summary>
public static class UpstreamGoneBranches
{
    /// <summary>The short names of the local branches that have <see cref="GitRef.IsUpstreamGone"/> set.</summary>
    public static IReadOnlySet<string> Of(IEnumerable<GitRef> refs) =>
        refs.Where(reference => reference.Kind == RefKind.LocalBranch && reference.IsUpstreamGone)
            .Select(reference => reference.Name)
            .ToImmutableHashSet(StringComparer.Ordinal);
}
