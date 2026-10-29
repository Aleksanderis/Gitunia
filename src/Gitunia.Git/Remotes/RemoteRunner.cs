using Gitunia.Git.Execution;
using Gitunia.Git.Execution.Models;
using Gitunia.Git.Remotes.Models;
using Gitunia.Git.Repositories.Models;

namespace Gitunia.Git.Remotes;

/// <summary>
/// Runs git commands that talk to a remote (fetch, pull, push): never read-only, git's <c>--progress</c> stderr is
/// parsed into <see cref="RemoteProgress"/>, the timeout is an idle limit (a long transfer is fine while it moves
/// data), a cancel kills the whole process tree, and ssh is told not to prompt (<see cref="SshCommandPolicy"/>).
/// </summary>
/// <param name="runner">The shared git runner.</param>
/// <param name="environmentLookup">Reads a process environment variable (the real lookup in production).</param>
public sealed class RemoteRunner(GitRunner runner, Func<string, string?> environmentLookup)
{
    public const string ProgressOption = "--progress";

    private const string SshCommandVariable = "GIT_SSH_COMMAND";
    private const string SshProgramVariable = "GIT_SSH";
    private const string Config = "config";
    private const string GetOption = "--get";
    private const string SshCommandKey = "core.sshCommand";

    private static readonly string[] SshConfigArguments = [Config, GetOption, SshCommandKey];

    private static readonly TimeSpan IdleTimeout = TimeSpan.FromMinutes(2);

    public RemoteRunner(GitRunner runner)
        : this(runner, Environment.GetEnvironmentVariable)
    {
    }

    /// <summary>
    /// Runs git with <paramref name="arguments"/>; a failure throws the typed git exception and a cancel throws
    /// <see cref="OperationCanceledException"/>.
    /// </summary>
    public async Task<GitResult> RunAsync(
        RepositoryLocation location,
        IReadOnlyList<string> arguments,
        IProgress<RemoteProgress>? progress,
        CancellationToken cancellationToken)
    {
        // A cancel before the start must not run a write that a short-lived process would finish before it is noticed.
        cancellationToken.ThrowIfCancellationRequested();
        var environment = await BuildEnvironmentAsync(location, cancellationToken).ConfigureAwait(false);
        var options = new GitRunOptions(
            Timeout: IdleTimeout,
            Environment: environment,
            OnStandardErrorLine: line => Report(progress, line));
        return await runner.RunAsync(location.CommandPath, arguments, options, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Runs git outside any repository (clone). The ssh policy reads <c>core.sshCommand</c> from the global
    /// configuration only; paths in <paramref name="arguments"/> must be absolute.
    /// </summary>
    public async Task<GitResult> RunGlobalAsync(
        IReadOnlyList<string> arguments,
        IProgress<RemoteProgress>? progress,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var environment = await BuildEnvironmentAsync(null, cancellationToken).ConfigureAwait(false);
        var options = new GitRunOptions(
            Timeout: IdleTimeout,
            Environment: environment,
            OnStandardErrorLine: line => Report(progress, line));
        return await runner.RunGlobalAsync(arguments, options, cancellationToken).ConfigureAwait(false);
    }

    private static void Report(IProgress<RemoteProgress>? progress, string line)
    {
        if (progress is not null && RemoteProgressParser.Parse(line) is { } parsed)
        {
            progress.Report(parsed);
        }
    }

    private async Task<IReadOnlyDictionary<string, string?>?> BuildEnvironmentAsync(
        RepositoryLocation? location,
        CancellationToken cancellationToken)
    {
        var sshCommand = environmentLookup(SshCommandVariable);
        var sshProgram = environmentLookup(SshProgramVariable);
        string? configured = null;

        // Git's order is the variable, then core.sshCommand, then GIT_SSH.
        if (string.IsNullOrWhiteSpace(sshCommand))
        {
            configured = await ReadConfiguredSshAsync(location, cancellationToken).ConfigureAwait(false);
        }

        if (SshCommandPolicy.BatchCommand(sshCommand, sshProgram, configured) is { } batch)
        {
            return new Dictionary<string, string?> { [SshCommandVariable] = batch };
        }

        return null;
    }

    private async Task<string> ReadConfiguredSshAsync(RepositoryLocation? location, CancellationToken cancellationToken)
    {
        if (location is null)
        {
            var global = await runner
                .RunGlobalAsync(SshConfigArguments, GitRunOptions.ReadOnlyRunAllowingNoMatch, cancellationToken)
                .ConfigureAwait(false);

            return global.StandardOutputText.Trim();
        }

        var result = await runner
            .RunAsync(
                location.CommandPath,
                SshConfigArguments,
                GitRunOptions.ReadOnlyRunAllowingNoMatch,
                cancellationToken)
            .ConfigureAwait(false);

        return result.StandardOutputText.Trim();
    }
}
