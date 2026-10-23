using Gitunia.Git;

namespace Gitunia.Core.Workspaces.Repositories;

/// <summary>Mark a commit as one end of a comparison of two commits, or clear the mark with null (#535).</summary>
/// <param name="Commit">The marked commit, null for none.</param>
public sealed record SetCompareMarkCommand(ObjectId? Commit) : RepositoryCommand;
