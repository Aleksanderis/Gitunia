namespace Gitunia.Git.Repositories.Models;

/// <summary>A multi-step git operation that is stopped and waiting for the user.</summary>
public enum RepositoryOperation
{
    None,
    Merge,
    Rebase,
    CherryPick,
    Revert,
    ApplyMailbox,
    Bisect,
}
