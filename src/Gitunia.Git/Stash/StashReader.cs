using Gitunia.Git.Execution;
using Gitunia.Git.Repositories;

namespace Gitunia.Git.Stash;

/// <summary>Reads the stash stack of a repository.</summary>
public static class StashReader
{
    private static readonly string[] Arguments = ["stash", "list", $"--format={StashParser.Format}"];

    public static async Task<ParseResult<StashEntry>> ReadStashesAsync(Repository repository, CancellationToken cancellationToken)
    {
        var result = await repository.RunAsync(Arguments, GitRunOptions.ReadOnlyRun, cancellationToken).ConfigureAwait(false);
        return StashParser.Parse(result.StandardOutputText);
    }
}
