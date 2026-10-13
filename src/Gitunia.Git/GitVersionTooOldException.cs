namespace Gitunia.Git;

/// <summary>The installed git is older than the supported minimum.</summary>
public sealed class GitVersionTooOldException : GitException
{
    public GitVersionTooOldException(GitVersion found, GitVersion minimum)
        : base($"git {found} is too old; Gitunia needs git {minimum} or newer.")
    {
        Found = found;
        Minimum = minimum;
    }

    public GitVersion Found { get; }

    public GitVersion Minimum { get; }
}
