namespace Gitunia.Git.Stash.Models;

/// <summary>The result of restoring one file from a stash.</summary>
/// <param name="State">What the restore found; anything but <c>Creates</c> or <c>Overwrites</c> means nothing was
/// written.</param>
/// <param name="BackupBlob">The object id under which the replaced working-tree content was saved in the object
/// database (<c>git cat-file blob &lt;id&gt;</c> prints it); null unless an existing file was replaced.</param>
public sealed record StashRestoreResult(StashRestoreState State, string? BackupBlob)
{
    /// <summary>Whether the working tree was changed.</summary>
    public bool Restored => State is StashRestoreState.Creates or StashRestoreState.Overwrites;
}
