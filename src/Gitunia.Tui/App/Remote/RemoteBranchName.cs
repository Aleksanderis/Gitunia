namespace Gitunia.Tui.App.Remote;

/// <summary>A remote-tracking branch split into its remote and the branch name on that remote.</summary>
/// <param name="Remote">The remote, for example <c>origin</c>.</param>
/// <param name="Branch">The branch on the remote, for example <c>feature/x</c>.</param>
public sealed record RemoteBranchName(string Remote, string Branch)
{
    /// <summary>
    /// Splits <c>origin/feature/x</c> using the configured remotes (a remote name may itself contain a slash, so the
    /// longest match wins); null when no remote is a prefix.
    /// </summary>
    public static RemoteBranchName? Split(string shortName, IReadOnlyList<string> remotes)
    {
        var remote = remotes
            .Where(candidate => shortName.StartsWith($"{candidate}/", StringComparison.Ordinal))
            .OrderByDescending(candidate => candidate.Length)
            .FirstOrDefault();
        return remote is null ? null : new RemoteBranchName(remote, shortName[(remote.Length + 1)..]);
    }
}
