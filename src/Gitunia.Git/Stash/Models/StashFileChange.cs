using Gitunia.Git.Commits.Models;

namespace Gitunia.Git.Stash.Models;

/// <summary>One file of a stash and where in the stash commit it comes from.</summary>
/// <param name="Change">Path, kind and rename information; an untracked file is always <c>Added</c>.</param>
/// <param name="Origin">Tracked change or saved untracked file.</param>
public sealed record StashFileChange(CommitFileChange Change, StashFileOrigin Origin);
