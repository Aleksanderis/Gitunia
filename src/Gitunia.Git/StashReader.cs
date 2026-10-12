namespace Gitunia.Git;

/// <summary>Reads the stash stack of a repository.</summary>
public static class StashReader
{
    private static readonly GitRunOptions PollingOptions = new(ReadOnly: true);

    public static async Task<ParseResult<StashEntry>> ReadStashesAsync(Repo repo, CancellationToken cancellationToken)
    {
        var result = await repo.RunAsync(["stash", "list", "--format=" + StashParser.Format], PollingOptions, cancellationToken).ConfigureAwait(false);
        return StashParser.Parse(result.StandardOutputText);
    }
}
