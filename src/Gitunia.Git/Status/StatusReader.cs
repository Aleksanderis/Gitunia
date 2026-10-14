using Gitunia.Git.Execution;
using Gitunia.Git.Repositories;
using Gitunia.Git.Status.Models;

namespace Gitunia.Git.Status;

/// <summary>Reads the working tree status of a repository or worktree.</summary>
public static class StatusReader
{
    private static readonly GitRunOptions PollingOptions = new(ReadOnly: true);

    /// <summary>Runs <c>status --porcelain=v2 -z --branch</c>; untracked files are listed individually unless <paramref name="untrackedFiles"/> says otherwise.</summary>
    public static async Task<StatusSnapshot> ReadStatusAsync(
        Repository repository,
        bool includeIgnored,
        CancellationToken cancellationToken,
        UntrackedFilesMode untrackedFiles = UntrackedFilesMode.All)
    {
        var untrackedArgument = untrackedFiles == UntrackedFilesMode.All ? "--untracked-files=all" : "--untracked-files=normal";
        var arguments = new List<string> { "status", "--porcelain=v2", GitOptions.NulTerminated, "--branch", untrackedArgument };
        if (includeIgnored)
        {
            arguments.Add("--ignored=traditional");
        }

        var result = await repository.RunAsync(arguments, PollingOptions, cancellationToken).ConfigureAwait(false);
        return StatusParser.Parse(result.StandardOutputText);
    }
}
