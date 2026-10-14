namespace Gitunia.Git.Diff.Models;

/// <summary>Which way a partial patch will be applied, which decides what unselected lines turn into.</summary>
public enum PatchDirection
{
    /// <summary>Applied as is (<c>git apply --cached</c>, to stage working-tree changes).</summary>
    Forward,

    /// <summary>Applied with <c>-R</c> (to unstage index changes or discard working-tree changes).</summary>
    Reverse,
}
