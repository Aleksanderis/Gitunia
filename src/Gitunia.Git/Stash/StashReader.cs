using Gitunia.Git.Execution;
using Gitunia.Git.Execution.Exceptions;
using Gitunia.Git.Repositories.Models;

namespace Gitunia.Git.Stash;

/// <summary>Reads the stash stack of a repository.</summary>
public sealed class StashReader : IStashReader
{
    private static readonly string[] Arguments = ["stash", "list", $"--format={StashParser.Format}"];

    private static readonly string[] StashRefArguments = ["rev-parse", "--quiet", "--verify", "refs/stash"];

    private readonly GitRunner _runner;

    public StashReader(GitRunner runner)
    {
        _runner = runner;
    }

    public async Task<ParseResult<StashEntry>> ReadStashesAsync(RepositoryLocation location, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _runner.RunAsync(location.CommandPath, Arguments, GitRunOptions.ReadOnlyRun, cancellationToken).ConfigureAwait(false);
            return StashParser.Parse(result.StandardOutputText);
        }
        catch (GitCommandFailedException)
        {
            // The last stash may have been dropped while "stash list" walked the reflog ("bad revision
            // 'refs/stash...'"): the stack is then empty, which is not a failure. Any other failure stays one.
            if (!await IsStashRefGoneAsync(location, cancellationToken).ConfigureAwait(false))
            {
                throw;
            }

            return StashParser.Parse(string.Empty);
        }
    }

    private async Task<bool> IsStashRefGoneAsync(RepositoryLocation location, CancellationToken cancellationToken)
    {
        var result = await _runner.RunAsync(location.CommandPath, StashRefArguments, GitRunOptions.ReadOnlyRunAllowingNoMatch, cancellationToken).ConfigureAwait(false);
        return result.ExitCode != 0;
    }
}
