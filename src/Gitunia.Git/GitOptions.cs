namespace Gitunia.Git;

/// <summary>Command line options shared by many git commands. Sub-command names stay at the call site.</summary>
public static class GitOptions
{
    /// <summary>NUL-terminated output.</summary>
    public const string NulTerminated = "-z";

    /// <summary>Never colorize, whatever <c>color.ui</c> says.</summary>
    public const string NoColor = "--no-color";

    /// <summary>Ends the options, so that a revision or path matching the other kind is not ambiguous.</summary>
    public const string EndOfOptions = "--";

    /// <summary>The revision name of the checked-out commit.</summary>
    public const string HeadRevision = "HEAD";

    /// <summary>Put in front of a revision to leave it and its ancestors out of a revision walk.</summary>
    public const string ExcludeRevisionPrefix = "^";

    /// <summary>Follow only the first parent; <c>git show</c> then diffs a merge against its mainline parent.</summary>
    public const string FirstParent = "--first-parent";

    /// <summary>Read the paths from stdin (with <see cref="PathspecFileNul"/> NUL-separated, so any byte sequence survives).</summary>
    public const string PathspecFromStandardInput = "--pathspec-from-file=-";

    /// <summary>Paths in the pathspec file are separated by NUL and not quoted.</summary>
    public const string PathspecFileNul = "--pathspec-file-nul";

    /// <summary>Compare two paths outside the index (how an untracked file is shown as additions).</summary>
    public const string NoIndex = "--no-index";

    /// <summary>Compare the index with HEAD.</summary>
    public const string Cached = "--cached";

    /// <summary>The pathspec prefix that takes the rest as a plain path: no glob, no magic.</summary>
    public const string LiteralPathspecPrefix = ":(literal)";

    /// <summary>The empty side of an added file; git's own literal, understood on every platform.</summary>
    public const string NullDevice = "/dev/null";

    /// <summary><c>git apply</c>: change the index only, not the working tree.</summary>
    public const string ApplyToIndex = "--cached";

    /// <summary><c>git apply</c>: undo the patch (unstage a hunk).</summary>
    public const string ApplyReverse = "-R";

    /// <summary><c>git apply</c>: no whitespace warnings for the lines it applies (the patch is a copy of what git printed).</summary>
    public const string ApplyNoWhitespaceWarning = "--whitespace=nowarn";

    /// <summary>Read the patch from stdin.</summary>
    public const string StandardInputPath = "-";
}
