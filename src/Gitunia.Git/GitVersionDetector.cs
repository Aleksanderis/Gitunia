namespace Gitunia.Git;

/// <summary>Detects the installed git version and enforces the minimum.</summary>
public static class GitVersionDetector
{
    /// <exception cref="GitVersionTooOldException">git is older than <see cref="GitVersion.Minimum"/>.</exception>
    /// <exception cref="GitNotFoundException">git cannot be started.</exception>
    public static async Task<GitVersion> DetectAsync(GitRunner runner, CancellationToken cancellationToken)
    {
        GitResult result = await runner.RunGlobalAsync(["--version"], GitRunOptions.Default, cancellationToken).ConfigureAwait(false);
        if (!GitVersion.TryParse(result.StandardOutputText, out GitVersion version))
        {
            throw new GitException($"Could not parse git version from '{result.StandardOutputText.Trim()}'.");
        }

        if (!version.IsAtLeast(GitVersion.Minimum))
        {
            throw new GitVersionTooOldException(version, GitVersion.Minimum);
        }

        return version;
    }
}
