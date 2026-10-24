namespace Gitunia.Git.Stash.Models;

/// <summary>
/// The changed files of a stash: the tracked changes first, then the untracked files, each in git's path order.
/// </summary>
/// <param name="CommitHash">The stash commit the files were read from.</param>
/// <param name="Files">Tracked changes followed by untracked files.</param>
public sealed record StashFiles(string CommitHash, IReadOnlyList<StashFileChange> Files);
