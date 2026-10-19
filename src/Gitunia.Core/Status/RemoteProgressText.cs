using Gitunia.Git.Remotes.Models;

namespace Gitunia.Core.Status;

/// <summary>The one status-bar line for a running fetch, pull or push (pure; the caller posts it).</summary>
public static class RemoteProgressText
{
    /// <summary>
    /// For example <c>Fetching: Receiving objects 45%</c>; <c>Fetching...</c> before git reported anything.
    /// </summary>
    /// <param name="activity">The running operation as a gerund (<c>Fetching</c>, <c>Pulling</c>).</param>
    /// <param name="progress">The latest report, or null.</param>
    public static string Format(string activity, RemoteProgress? progress)
    {
        if (progress is null)
        {
            return $"{activity}...";
        }

        return progress.Percent is { } percent
            ? $"{activity}: {progress.Stage} {percent}%"
            : $"{activity}: {progress.Stage}";
    }
}
