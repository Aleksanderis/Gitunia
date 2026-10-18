using Gitunia.Git.Execution;
using Gitunia.Git.Remotes.Models;
using Gitunia.Git.Repositories.Models;

namespace Gitunia.Git.Remotes;

/// <summary>Remote management with the git CLI; config, hooks and the user's remote layout are left alone.</summary>
public sealed class RemoteManager(GitRunner runner) : IRemoteManager
{
    private const string Remote = "remote";
    private const string Config = "config";
    private const string GetRegexpOption = "--get-regexp";
    private const string UrlPattern = "^remote\\..*\\.url$";
    private const string UrlKeyPrefix = "remote.";
    private const string UrlKeySuffix = ".url";
    private const string AddCommand = "add";
    private const string RemoveCommand = "remove";
    private const string RenameCommand = "rename";
    private const string SetUrlCommand = "set-url";
    private const string EndOfOptions = "--";
    private const char LineSeparator = '\n';

    private static readonly TimeSpan WriteTimeout = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan ReadTimeout = TimeSpan.FromSeconds(30);

    private static readonly GitRunOptions ReadOptions = new(Timeout: ReadTimeout, ReadOnly: true);
    // Exit code 1 means no remote has a URL, which is an answer.
    private static readonly GitRunOptions UrlOptions = new(
        Timeout: ReadTimeout,
        ReadOnly: true,
        AcceptedExitCodes: [1]);

    private static readonly GitRunOptions WriteOptions = new(Timeout: WriteTimeout);

    public async Task<IReadOnlyList<RemoteInfo>> ListAsync(
        RepositoryLocation location,
        CancellationToken cancellationToken)
    {
        var names = await runner
            .RunAsync(location.CommandPath, [Remote], ReadOptions, cancellationToken)
            .ConfigureAwait(false);

        // The raw configured URLs: "remote -v" would show them after url.<base>.insteadOf rewriting.
        var configured = await runner
            .RunAsync(location.CommandPath, [Config, GetRegexpOption, UrlPattern], UrlOptions, cancellationToken)
            .ConfigureAwait(false);
        var urls = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var line in configured.StandardOutputText.Split(LineSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = line.IndexOf(' ', StringComparison.Ordinal);
            if (separator > UrlKeyPrefix.Length + UrlKeySuffix.Length
                && line.StartsWith(UrlKeyPrefix, StringComparison.Ordinal))
            {
                var key = line[..separator];
                if (key.EndsWith(UrlKeySuffix, StringComparison.Ordinal))
                {
                    urls.TryAdd(key[UrlKeyPrefix.Length..^UrlKeySuffix.Length], line[(separator + 1)..].Trim());
                }
            }
        }

        return
        [
            .. names.StandardOutputText
                .Split(LineSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(name => new RemoteInfo(name, urls.GetValueOrDefault(name, string.Empty))),
        ];
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
