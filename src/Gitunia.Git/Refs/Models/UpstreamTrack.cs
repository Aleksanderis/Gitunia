namespace Gitunia.Git.Refs.Models;

/// <summary>How a local branch relates to its upstream.</summary>
/// <param name="Ahead">Commits ahead of the upstream, null when there is no upstream or it is gone.</param>
/// <param name="Behind">Commits behind the upstream, null when there is no upstream or it is gone.</param>
/// <param name="IsGone">The configured upstream no longer exists.</param>
public sealed record UpstreamTrack(int? Ahead, int? Behind, bool IsGone);
