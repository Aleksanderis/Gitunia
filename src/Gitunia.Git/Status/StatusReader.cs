using Gitunia.Git.Execution;
using Gitunia.Git.Repositories.Models;
using Gitunia.Git.Status.Models;

namespace Gitunia.Git.Status;

/// <summary>Reads the working tree status of a repository or worktree.</summary>
public sealed class StatusReader : IStatusReader
{
    private const string UntrackedAll = "--untracked-files=all";
    private const string UntrackedNormal = "--untracked-files=normal";
    private const string IgnoredTraditional = "--ignored=traditional";

    private static readonly string[] FixedArguments = ["status", "--porcelain=v2", GitOptions.NulTerminated, "--branch"];

    private readonly GitRunner _runner;

    public StatusReader(GitRunner runner)
    {
        _runner = runner;
    }

    /// <summary>Runs <c>status --porcelain=v2 -z --branch</c>; untracked files are listed individually unless <paramref name="untrackedFiles"/> says otherwise.</summary>
    public async Task<StatusSnapshot> ReadStatusAsync(
        RepositoryLocation location,
        bool includeIgnored,
        UntrackedFilesMode untrackedFiles,
        CancellationToken cancellationToken)
    {
        var untrackedArgument = untrackedFiles == UntrackedFilesMode.All ? UntrackedAll : UntrackedNormal;
        string[] arguments = [.. FixedArguments, untrackedArgument];
        if (includeIgnored)
        {
            arguments = [.. FixedArguments, untrackedArgument, IgnoredTraditional];
        }

        var result = await _runner.RunAsync(location.CommandPath, arguments, GitRunOptions.ReadOnlyRun, cancellationToken).ConfigureAwait(false);
        return StatusParser.Parse(GitPathText.Decode(result.StandardOutput));
    }
}
