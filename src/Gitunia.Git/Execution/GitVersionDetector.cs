using Gitunia.Git.Execution.Exceptions;
using Gitunia.Git.Execution.Models;

namespace Gitunia.Git.Execution;

/// <summary>Detects the installed git version and enforces the minimum.</summary>
public sealed class GitVersionDetector
{
    private readonly GitRunner _runner;

    public GitVersionDetector(GitRunner runner)
    {
        _runner = runner;
    }

    /// <exception cref="GitVersionTooOldException">Git is older than <see cref="GitVersion.Minimum"/>.</exception>
    /// <exception cref="GitNotFoundException">Git cannot be started.</exception>
    public async Task<GitVersion> DetectAsync(CancellationToken cancellationToken)
    {
        var result = await _runner.RunGlobalAsync(["--version"], GitRunOptions.Default, cancellationToken).ConfigureAwait(false);
        if (!GitVersion.TryParse(result.StandardOutputText, out var version))
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
