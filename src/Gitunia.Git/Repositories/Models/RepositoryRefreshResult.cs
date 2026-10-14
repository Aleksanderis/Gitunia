namespace Gitunia.Git.Repositories.Models;

/// <summary>The outcome of a refresh: the new snapshot and which sections differ from the previous one (all of them for a first load).</summary>
public sealed record RepositoryRefreshResult(RepositorySnapshot Snapshot, RepositorySections Changed);
