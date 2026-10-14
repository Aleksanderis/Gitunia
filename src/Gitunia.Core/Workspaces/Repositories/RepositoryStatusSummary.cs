using Gitunia.Git.Status.Models;

namespace Gitunia.Core.Workspaces.Repositories;

/// <summary>The few facts about the working tree the tab bar shows.</summary>
/// <param name="IsDirty">There are changed, staged, untracked or conflicted files.</param>
/// <param name="Ahead">Commits ahead of the upstream, 0 without an upstream.</param>
/// <param name="Behind">Commits behind the upstream, 0 without an upstream.</param>
public sealed record RepositoryStatusSummary(bool IsDirty, int Ahead, int Behind)
{
    /// <summary>Summarizes a full status read.</summary>
    public static RepositoryStatusSummary From(StatusSnapshot status) => new(status.Entries.Count > 0, status.Branch.Ahead ?? 0, status.Branch.Behind ?? 0);
}
