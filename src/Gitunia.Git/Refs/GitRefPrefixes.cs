namespace Gitunia.Git.Refs;

/// <summary>Prefixes of full ref names.</summary>
public static class GitRefPrefixes
{
    /// <summary>The namespace of all refs.</summary>
    public const string Root = "refs/";

    /// <summary>Local branches.</summary>
    public const string Heads = "refs/heads/";

    /// <summary>Remote-tracking branches.</summary>
    public const string Remotes = "refs/remotes/";

    /// <summary>Tags.</summary>
    public const string Tags = "refs/tags/";
}
