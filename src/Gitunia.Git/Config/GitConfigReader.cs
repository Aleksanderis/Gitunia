using Gitunia.Git.Execution;
using Gitunia.Git.Repositories.Models;

namespace Gitunia.Git.Config;

/// <summary>Reads settings of the user's git config.</summary>
public sealed class GitConfigReader : IGitConfigReader
{
    private const string TrueValue = "true";

    private static readonly string[] RebaseAutoStashArguments = ["config", "--type=bool", "--get", "rebase.autoStash"];

    private readonly GitRunner _runner;

    public GitConfigReader(GitRunner runner)
    {
        _runner = runner;
    }

    public async Task<bool> ReadRebaseAutoStashAsync(RepositoryLocation location, CancellationToken cancellationToken)
    {
        var result = await _runner
            .RunAsync(
                location.CommandPath,
                RebaseAutoStashArguments,
                GitRunOptions.ReadOnlyRunAllowingNoMatch,
                cancellationToken)
            .ConfigureAwait(false);
        return result.ExitCode == 0 && result.StandardOutputText.Trim() == TrueValue;
    }
}
