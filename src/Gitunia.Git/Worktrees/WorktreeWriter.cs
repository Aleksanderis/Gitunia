using Gitunia.Git.Execution;
using Gitunia.Git.Execution.Models;
using Gitunia.Git.Repositories.Models;
using Gitunia.Git.Worktrees.Models;

namespace Gitunia.Git.Worktrees;

/// <summary>
/// Changes worktrees with the git CLI. Paths and names are arguments after <c>--</c> where git allows it, so a name that starts
/// with a dash is never taken as an option; hooks (<c>post-checkout</c>) run in the user's environment.
/// </summary>
public sealed class WorktreeWriter(GitRunner runner) : IWorktreeWriter
{
    private const string Worktree = "worktree";
    private const string NewBranchOption = "-b";
    private const string ForceOption = "--force";
    private const string ReasonOption = "--reason";
    private const string VerboseOption = "--verbose";
    private const char NulSeparator = '\0';
    private const char LineSeparator = '\n';

    // Porcelain status records are "XY path": two status letters and a space.
    private const int StatusPrefixLength = 3;

    private static readonly TimeSpan WriteTimeout = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan ReadTimeout = TimeSpan.FromSeconds(30);
    private static readonly string[] PruneArguments = [Worktree, "prune", VerboseOption];
    private static readonly string[] ChangesArguments =
        ["status", "--porcelain", GitOptions.NulTerminated, "--no-renames", "--untracked-files=normal"];

    public async Task AddAsync(RepositoryLocation location, WorktreeAddRequest request, CancellationToken cancellationToken)
    {
        string[] arguments = request.NewBranchStartPoint is { } startPoint
            ? [Worktree, "add", NewBranchOption, request.Branch, GitOptions.EndOfOptions, request.Path, startPoint]
            : [Worktree, "add", GitOptions.EndOfOptions, request.Path, request.Branch];
        await RunAsync(location.CommandPath, arguments, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<string>> ListChangesAsync(string path, CancellationToken cancellationToken)
    {
        var options = new GitRunOptions(Timeout: ReadTimeout, ReadOnly: true);
        var result = await runner.RunAsync(path, ChangesArguments, options, cancellationToken).ConfigureAwait(false);
        return
        [
            .. result.StandardOutputText
                .Split(NulSeparator, StringSplitOptions.RemoveEmptyEntries)
                .Where(record => record.Length > StatusPrefixLength)
                .Select(record => record[StatusPrefixLength..]),
        ];
    }

    public async Task RemoveAsync(
        RepositoryLocation location,
        string path,
        bool force,
        CancellationToken cancellationToken)
    {
        string[] arguments = force
            ? [Worktree, "remove", ForceOption, GitOptions.EndOfOptions, path]
            : [Worktree, "remove", GitOptions.EndOfOptions, path];
        await RunAsync(location.CommandPath, arguments, cancellationToken).ConfigureAwait(false);
    }

    public async Task LockAsync(
        RepositoryLocation location,
        string path,
        string? reason,
        CancellationToken cancellationToken)
    {
        string[] arguments = string.IsNullOrWhiteSpace(reason)
            ? [Worktree, "lock", GitOptions.EndOfOptions, path]
            : [Worktree, "lock", ReasonOption, reason.Trim(), GitOptions.EndOfOptions, path];
        await RunAsync(location.CommandPath, arguments, cancellationToken).ConfigureAwait(false);
    }

    public async Task UnlockAsync(RepositoryLocation location, string path, CancellationToken cancellationToken)
    {
        string[] arguments = [Worktree, "unlock", GitOptions.EndOfOptions, path];
        await RunAsync(location.CommandPath, arguments, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<string>> PruneAsync(RepositoryLocation location, CancellationToken cancellationToken)
    {
        var result = await RunAsync(location.CommandPath, PruneArguments, cancellationToken).ConfigureAwait(false);

        // Verbose prune reports "Removing worktrees/<name>: <reason>" on stderr.
        return
        [
            .. result.StandardError.Split(LineSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
        ];
    }

    private Task<GitResult> RunAsync(string commandPath, string[] arguments, CancellationToken cancellationToken) =>
        runner.RunAsync(commandPath, arguments, new GitRunOptions(Timeout: WriteTimeout), cancellationToken);
}
