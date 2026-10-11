namespace Gitunia.Git;

/// <summary>Reads the working tree status of a repository or worktree.</summary>
public static class StatusReader
{
    private static readonly GitRunOptions PollingOptions = new(ReadOnly: true);

    /// <summary>Runs <c>status --porcelain=v2 -z --branch</c> with every untracked file listed individually.</summary>
    public static async Task<StatusSnapshot> ReadStatusAsync(Repo repo, bool includeIgnored, CancellationToken cancellationToken)
    {
        var arguments = new List<string> { "status", "--porcelain=v2", "-z", "--branch", "--untracked-files=all" };
        if (includeIgnored)
        {
            arguments.Add("--ignored=traditional");
        }

        var result = await repo.RunAsync(arguments, PollingOptions, cancellationToken).ConfigureAwait(false);
        return StatusParser.Parse(result.StandardOutputText);
    }
}
