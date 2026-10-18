namespace Gitunia.Git.Remotes.Models;

/// <summary>What a fetch updates.</summary>
/// <param name="Remote">The remote to fetch, or null for the current branch's remote (git's own default).</param>
/// <param name="AllRemotes">Fetch every remote; <paramref name="Remote"/> is then ignored.</param>
/// <param name="Prune">Remove remote-tracking branches that no longer exist on the remote.</param>
public sealed record FetchRequest(string? Remote, bool AllRemotes, bool Prune);
