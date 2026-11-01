using Gitunia.Git.Refs.Models;

namespace Gitunia.Core.Sync;

/// <summary>A remote-tracking branch split into remote and branch, with the commit it was at.</summary>
/// <param name="Name">The remote and the branch on it.</param>
/// <param name="CommitHash">The tip we saw; the delete is guarded by it.</param>
public sealed record RemoteBranchRef(RemoteBranchName Name, string CommitHash);
