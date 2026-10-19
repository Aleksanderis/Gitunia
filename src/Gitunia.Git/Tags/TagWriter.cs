using Gitunia.Git.Execution;
using Gitunia.Git.Refs;
using Gitunia.Git.Remotes;
using Gitunia.Git.Repositories.Models;

namespace Gitunia.Git.Tags;

/// <summary>
/// Tag operations with the git CLI; hooks, config and credential helpers are the user's. Pushes go through the
/// remote runner (idle timeout, ssh never prompts).
/// </summary>
public sealed class TagWriter(GitRunner runner, RemoteRunner remoteRunner) : ITagWriter
{
    private const string Tag = "tag";
    private const string Push = "push";
    private const string Remote = "remote";
    private const string CheckRefFormat = "check-ref-format";
    private const string ConfigOption = "-c";
    private const string NoEditor = "core.editor=true";
    private const string AnnotateOption = "-a";
    private const string MessageOption = "-m";
    private const string DeleteOption = "-d";
    private const string RemoteDeleteOption = "--delete";
    private const string NoProgressOption = "--no-progress";
    private const string EndOfOptions = "--";
    private const string LeadingDash = "-";
    private const char LineSeparator = '\n';

    // check-ref-format answers an invalid name with exit code 1; that is an answer, not a failure.
    private const int InvalidNameExitCode = 1;

    private static readonly TimeSpan WriteTimeout = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan ReadTimeout = TimeSpan.FromSeconds(30);

    private static readonly GitRunOptions InvalidNameAllowed = new(
        Timeout: ReadTimeout,
        ReadOnly: true,
        AcceptedExitCodes: [InvalidNameExitCode]);

    private static readonly GitRunOptions ReadOptions = new(Timeout: ReadTimeout, ReadOnly: true);
    private static readonly GitRunOptions WriteOptions = new(Timeout: WriteTimeout);

    public async Task<bool> IsValidNameAsync(
        RepositoryLocation location,
        string name,
        CancellationToken cancellationToken)
    {
        if (name.Length == 0 || name.StartsWith(LeadingDash, StringComparison.Ordinal))
        {
            return false;
        }

        var result = await runner
            .RunAsync(
                location.CommandPath,
                [CheckRefFormat, $"{GitRefPrefixes.Tags}{name}"],
                InvalidNameAllowed,
                cancellationToken)
            .ConfigureAwait(false);
        return result.ExitCode == 0;
    }

    public Task CreateAsync(
        RepositoryLocation location,
        string name,
        string target,
        string? message,
        CancellationToken cancellationToken) =>
        RunWriteAsync(
            location,
            string.IsNullOrEmpty(message)
                ? [Tag, EndOfOptions, name, target]
                : [ConfigOption, NoEditor, Tag, AnnotateOption, MessageOption, message, EndOfOptions, name, target],
            cancellationToken);

    public Task DeleteAsync(RepositoryLocation location, string name, CancellationToken cancellationToken) =>
        RunWriteAsync(location, [Tag, DeleteOption, EndOfOptions, name], cancellationToken);

    public async Task<IReadOnlyList<string>> ListRemotesAsync(
        RepositoryLocation location,
        CancellationToken cancellationToken)
    {
        var result = await runner.RunAsync(location.CommandPath, [Remote], ReadOptions, cancellationToken)
            .ConfigureAwait(false);
        return result.StandardOutputText
            .Split(LineSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    public async Task PushAsync(
        RepositoryLocation location,
        string remote,
        string name,
        CancellationToken cancellationToken) =>
        await remoteRunner
            .RunAsync(
                location,
                [Push, NoProgressOption, EndOfOptions, remote, $"{GitRefPrefixes.Tags}{name}"],
                null,
                cancellationToken)
            .ConfigureAwait(false);

    public async Task DeleteOnRemoteAsync(
        RepositoryLocation location,
        string remote,
        string name,
        CancellationToken cancellationToken) =>
        await remoteRunner
            .RunAsync(
                location,
                [Push, NoProgressOption, RemoteDeleteOption, EndOfOptions, remote, $"{GitRefPrefixes.Tags}{name}"],
                null,
                cancellationToken)
            .ConfigureAwait(false);

    private async Task RunWriteAsync(
        RepositoryLocation location,
        string[] arguments,
        CancellationToken cancellationToken)
    {
        // A cancel before the start must not run a write that a short-lived process would finish before it is noticed.
        cancellationToken.ThrowIfCancellationRequested();
        await runner.RunAsync(location.CommandPath, arguments, WriteOptions, cancellationToken).ConfigureAwait(false);
    }
}
