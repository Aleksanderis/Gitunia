using Gitunia.Git.Execution;
using Gitunia.Git.Execution.Exceptions;
using Gitunia.Git.Refs;
using Gitunia.Git.Repositories.Models;

namespace Gitunia.Git.Branches;

/// <summary>Branch operations with the git CLI; hooks, config and worktree layout are the user's.</summary>
public sealed class BranchWriter(GitRunner runner) : IBranchWriter
{
    private const string Branch = "branch";
    private const string Switch = "switch";
    private const string CheckRefFormat = "check-ref-format";
    private const string RevList = "rev-list";
    private const string BranchOption = "--branch";
    private const string CreateOption = "-c";
    private const string TrackOption = "--track";
    private const string DetachOption = "--detach";
    private const string MoveOption = "-m";
    private const string DeleteOption = "-d";
    private const string ForceDeleteOption = "-D";
    private const string FirstOnly = "-1";
    private const string SetUpstreamOption = "--set-upstream-to=";
    private const string UnsetUpstreamOption = "--unset-upstream";
    private const string Fetch = "fetch";
    private const string LocalSource = ".";
    private const string LeadingDash = "-";

    private static readonly TimeSpan WriteTimeout = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan ReadTimeout = TimeSpan.FromSeconds(30);

    // check-ref-format --branch answers an invalid name with a fatal error (128); that is an answer, not a failure.
    private const int InvalidNameExitCode = 128;

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
            .RunAsync(location.CommandPath, [CheckRefFormat, BranchOption, name], InvalidNameAllowed, cancellationToken)
            .ConfigureAwait(false);
        return result.ExitCode == 0;
    }

    public Task CreateAsync(
        RepositoryLocation location,
        string name,
        string startPoint,
        bool checkout,
        CancellationToken cancellationToken) =>
        RunWriteAsync(
            location,
            checkout ? [Switch, CreateOption, name, startPoint] : [Branch, name, startPoint],
            cancellationToken);

    public Task SwitchAsync(RepositoryLocation location, string name, CancellationToken cancellationToken) =>
        RunWriteAsync(location, [Switch, name], cancellationToken);

    public Task SwitchToRemoteAsync(
        RepositoryLocation location,
        string remoteBranch,
        CancellationToken cancellationToken) =>
        RunWriteAsync(location, [Switch, TrackOption, remoteBranch], cancellationToken);

    public Task SwitchDetachedAsync(RepositoryLocation location, string commit, CancellationToken cancellationToken) =>
        RunWriteAsync(location, [Switch, DetachOption, commit], cancellationToken);

    public Task RenameAsync(
        RepositoryLocation location,
        string oldName,
        string newName,
        CancellationToken cancellationToken) =>
        RunWriteAsync(location, [Branch, MoveOption, oldName, newName], cancellationToken);

    public async Task<bool> IsMergedAsync(
        RepositoryLocation location,
        string name,
        string? upstreamFullName,
        CancellationToken cancellationToken)
    {
        List<string> arguments =
        [
            RevList,
            FirstOnly,
            $"{GitRefPrefixes.Heads}{name}",
            $"{GitOptions.ExcludeRevisionPrefix}{GitOptions.HeadRevision}",
        ];
        if (upstreamFullName is not null)
        {
            arguments.Add($"{GitOptions.ExcludeRevisionPrefix}{upstreamFullName}");
        }

        try
        {
            var result = await runner.RunAsync(location.CommandPath, arguments, ReadOptions, cancellationToken)
                .ConfigureAwait(false);
            return result.StandardOutput.Length == 0;
        }
        catch (GitCommandFailedException)
        {
            // An unborn HEAD or a vanished upstream: unknown counts as unmerged, so the caller warns.
            return false;
        }
    }

    public Task SetUpstreamAsync(
        RepositoryLocation location,
        string name,
        string upstream,
        CancellationToken cancellationToken) =>
        RunWriteAsync(location, [Branch, $"{SetUpstreamOption}{upstream}", name], cancellationToken);

    public Task UnsetUpstreamAsync(RepositoryLocation location, string name, CancellationToken cancellationToken) =>
        RunWriteAsync(location, [Branch, UnsetUpstreamOption, name], cancellationToken);

    public Task FastForwardAsync(
        RepositoryLocation location,
        string name,
        string upstreamFullName,
        CancellationToken cancellationToken) =>
        // A fetch from this repository with a refspec that has no leading '+': git itself refuses anything but a
        // fast-forward and a branch that is checked out in any worktree. No network, no FETCH_HEAD, no tags.
        RunWriteAsync(
            location,
            [
                Fetch, "--no-tags", "--no-write-fetch-head", "--no-recurse-submodules", LocalSource,
                $"{upstreamFullName}:{GitRefPrefixes.Heads}{name}",
            ],
            cancellationToken);

    public Task DeleteAsync(
        RepositoryLocation location,
        string name,
        bool force,
        CancellationToken cancellationToken) =>
        RunWriteAsync(location, [Branch, force ? ForceDeleteOption : DeleteOption, name], cancellationToken);

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
