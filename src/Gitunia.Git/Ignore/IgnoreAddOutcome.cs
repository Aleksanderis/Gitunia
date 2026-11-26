namespace Gitunia.Git.Ignore;

/// <summary>What adding a pattern to the <c>.gitignore</c> of a working tree did.</summary>
public enum IgnoreAddOutcome
{
    /// <summary>The pattern was appended as a new line.</summary>
    Added,

    /// <summary>A line with exactly this pattern was already in the file; nothing was written.</summary>
    AlreadyListed,
}
