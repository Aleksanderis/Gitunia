namespace Gitunia.Git.Watching.Models;

/// <summary>A directory the repository watcher observes.</summary>
/// <param name="Path">The directory.</param>
/// <param name="IsRecursive">Whether subdirectories are watched too.</param>
public sealed record WatchTarget(string Path, bool IsRecursive);
