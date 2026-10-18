namespace Gitunia.Git.Remotes.Models;

/// <summary>How a fast-forward-only pull ended.</summary>
public enum PullOutcome
{
    UpToDate,
    FastForwarded,
}
