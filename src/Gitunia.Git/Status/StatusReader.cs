using Gitunia.Git.Execution;
using Gitunia.Git.Repositories;
using Gitunia.Git.Status.Models;

namespace Gitunia.Git.Status;

/// <summary>Reads the working tree status of a repository or worktree.</summary>
public static class StatusReader
{
    private const string UntrackedAll = "--untracked-files=all";
    private const string UntrackedNormal = "--untracked-files=normal";
    private const string IgnoredTraditional = "--ignored=traditional";

    private static readonly string[] FixedArguments = ["status", "--porcelain=v2", GitOptions.NulTerminated, "--branch"];

    /// <summary>Runs <c>status --porcelain=v2 -z --branch</c>; untracked files are listed individually unless <paramref name="untrackedFiles"/> says otherwise.</summary>
    public static async Task<StatusSnapshot> ReadStatusAsync(
        Repository repository,
        bool includeIgnored,
        CancellationToken cancellationToken,
        UntrackedFilesMode untrackedFiles = UntrackedFilesMode.All)
    {
        var untrackedArgument = untrackedFiles == UntrackedFilesMode.All ? UntrackedAll : UntrackedNormal;
        string[] arguments = includeIgnored
            ? [.. FixedArguments, untrackedArgument, IgnoredTraditional]
            : [.. FixedArguments, untrackedArgument];

        var result = await repository.RunAsync(arguments, GitRunOptions.ReadOnlyRun, cancellationToken).ConfigureAwait(false);
        return StatusParser.Parse(result.StandardOutputText);
    }
}
