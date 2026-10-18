namespace Gitunia.Git.Remotes.Models;

/// <summary>
/// A force push that is only allowed while the remote branch still has the commit we saw (<c>--force-with-lease</c>
/// with an explicit expectation), so a commit someone else pushed meanwhile is never overwritten.
/// </summary>
/// <param name="RemoteBranch">The short name of the branch on the remote, for example <c>main</c>.</param>
/// <param name="ExpectedHash">The commit the remote branch is expected to be at (our remote-tracking branch).</param>
public sealed record PushLease(string RemoteBranch, string ExpectedHash);
