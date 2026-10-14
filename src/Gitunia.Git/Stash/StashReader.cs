using Gitunia.Git.Execution;
using Gitunia.Git.Repositories.Models;

namespace Gitunia.Git.Stash;

/// <summary>Reads the stash stack of a repository.</summary>
public sealed class StashReader : IStashReader
{
    private static readonly string[] Arguments = ["stash", "list", $"--format={StashParser.Format}"];

    private readonly GitRunner _runner;

    public StashReader(GitRunner runner)
    {
        _runner = runner;
    }

    public async Task<ParseResult<StashEntry>> ReadStashesAsync(RepositoryLocation location, CancellationToken cancellationToken)
    {
        var result = await _runner.RunAsync(location.CommandPath, Arguments, GitRunOptions.ReadOnlyRun, cancellationToken).ConfigureAwait(false);
        return StashParser.Parse(result.StandardOutputText);
    }
}
