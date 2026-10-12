namespace Gitunia.Git;

/// <summary>One entry of <c>git stash list</c>.</summary>
/// <param name="Index">Position in the stash stack, 0 is the newest.</param>
/// <param name="Name">The stash reference, for example <c>stash@{0}</c>.</param>
/// <param name="CommitHash">The stash commit.</param>
/// <param name="Message">The reflog subject, for example <c>WIP on main: 1a2b3c4 subject</c> or <c>On main: custom message</c>.</param>
public sealed record StashEntry(int Index, string Name, string CommitHash, string Message);
