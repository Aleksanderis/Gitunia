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
}
