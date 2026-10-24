namespace Gitunia.Git.Stash.Models;

/// <summary>Which part of a stash commit a file comes from.</summary>
public enum StashFileOrigin
{
    /// <summary>A tracked file changed against the stash's base commit (<c>stash^1..stash</c>).</summary>
    Tracked,

    /// <summary>An untracked file saved by <c>--include-untracked</c>, held by the third parent.</summary>
    Untracked,
}
