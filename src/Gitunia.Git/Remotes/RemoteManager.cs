using Gitunia.Git.Execution;
using Gitunia.Git.Remotes.Models;
using Gitunia.Git.Repositories.Models;

namespace Gitunia.Git.Remotes;

/// <summary>Remote management with the git CLI; config, hooks and the user's remote layout are left alone.</summary>
public sealed class RemoteManager(GitRunner runner) : IRemoteManager
{
    private const string Remote = "remote";
    private const string VerboseOption = "-v";
    private const string AddCommand = "add";
    private const string RemoveCommand = "remove";
    private const string RenameCommand = "rename";
    private const string SetUrlCommand = "set-url";
    private const string EndOfOptions = "--";
    private const string FetchSuffix = " (fetch)";
    private const char LineSeparator = '\n';
    private const char FieldSeparator = '\t';

    private static readonly TimeSpan WriteTimeout = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan ReadTimeout = TimeSpan.FromSeconds(30);

    private static readonly GitRunOptions ReadOptions = new(Timeout: ReadTimeout, ReadOnly: true);
    private static readonly GitRunOptions WriteOptions = new(Timeout: WriteTimeout);

    public async Task<IReadOnlyList<RemoteInfo>> ListAsync(
        RepositoryLocation location,
        CancellationToken cancellationToken)
    {
        var result = await runner
            .RunAsync(location.CommandPath, [Remote, VerboseOption], ReadOptions, cancellationToken)
            .ConfigureAwait(false);
        var remotes = new List<RemoteInfo>();
        foreach (var line in result.StandardOutputText.Split(LineSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = line.IndexOf(FieldSeparator, StringComparison.Ordinal);
            if (separator <= 0 || !line.EndsWith(FetchSuffix, StringComparison.Ordinal))
            {
                continue;
            }

            remotes.Add(new RemoteInfo(line[..separator], line[(separator + 1)..^FetchSuffix.Length].Trim()));
        }

        return remotes;
    }

    public Task AddAsync(RepositoryLocation location, string name, string url, CancellationToken cancellationToken) =>
        RunWriteAsync(location, [Remote, AddCommand, EndOfOptions, name, url], cancellationToken);

    public Task RemoveAsync(RepositoryLocation location, string name, CancellationToken cancellationToken) =>
        RunWriteAsync(location, [Remote, RemoveCommand, EndOfOptions, name], cancellationToken);

    public Task RenameAsync(
        RepositoryLocation location,
        string name,
        string newName,
        CancellationToken cancellationToken) =>
        RunWriteAsync(location, [Remote, RenameCommand, EndOfOptions, name, newName], cancellationToken);

    public Task SetUrlAsync(
        RepositoryLocation location,
        string name,
        string url,
        CancellationToken cancellationToken) =>
        RunWriteAsync(location, [Remote, SetUrlCommand, EndOfOptions, name, url], cancellationToken);

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
