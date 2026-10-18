namespace Gitunia.Git.Remotes.Models;

/// <summary>What a push sends: git's default for the current branch (<c>push.default</c> and its upstream).</summary>
/// <param name="Remote">The remote to push to, or null for the branch's own remote.</param>
public sealed record PushRequest(string? Remote);
