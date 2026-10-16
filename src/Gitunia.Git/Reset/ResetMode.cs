namespace Gitunia.Git.Reset;

/// <summary>What <c>git reset</c> moves besides the branch.</summary>
public enum ResetMode
{
    /// <summary>Only the branch: the index and the working tree stay (<c>--soft</c>).</summary>
    Soft,

    /// <summary>The branch and the index; the working tree stays (<c>--mixed</c>).</summary>
    Mixed,

    /// <summary>The branch, the index and the tracked files of the working tree (<c>--hard</c>).</summary>
    Hard,
}
