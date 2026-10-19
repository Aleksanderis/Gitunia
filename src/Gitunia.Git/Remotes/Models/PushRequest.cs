namespace Gitunia.Git.Remotes.Models;

/// <summary>
/// What a push sends: by default git's own choice for the current branch (<c>push.default</c> and its upstream).
/// </summary>
/// <param name="Remote">The remote to push to, or null for the branch's own remote.</param>
/// <param name="Branch">The local branch to push (needed for a first push or a lease), or null.</param>
/// <param name="SetUpstream">Make the remote branch the upstream of <paramref name="Branch"/> (first push).</param>
/// <param name="Lease">Overwrite the remote branch, but only while it still is where we last saw it, or null.</param>
public sealed record PushRequest(
    string? Remote,
    string? Branch = null,
    bool SetUpstream = false,
    PushLease? Lease = null);
