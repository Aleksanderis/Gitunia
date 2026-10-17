using System.Globalization;
using Gitunia.Git.Execution;
using Gitunia.Git.Operations;
using Gitunia.Git.Operations.Models;
using Gitunia.Git.Repositories.Models;

namespace Gitunia.Git.Rebase;

/// <summary>
/// Rebases with the git CLI. <c>-c core.editor=true</c> keeps any message edit from opening an editor (it is passed per
/// call and never written); no <c>--interactive</c>, so no todo list is opened (interactive rebase is #399). Hooks and
/// config are the user's.
/// </summary>
public sealed class RebaseWriter(OperationRunner runner, GitRunner git) : IRebaseWriter
{
    private static readonly GitRunOptions CountOptions = new(Timeout: TimeSpan.FromSeconds(30), ReadOnly: true);

    private const string CommitSuffix = "^{commit}";

    public Task<OperationResult> RebaseAsync(
        RepositoryLocation location,
        string onto,
        CancellationToken cancellationToken)
    {
        if (onto.StartsWith('-'))
        {
            throw new ArgumentException("A rebase target cannot start with '-'", nameof(onto));
        }

        // A cancel before the start must not run a write that a short-lived process would finish before it is noticed.
        cancellationToken.ThrowIfCancellationRequested();

        // The suffix makes git take only a commit (a tag object is peeled), never an option.
        return runner.RunAsync(
            location,
            ["-c", "core.editor=true", "rebase", $"{onto}{CommitSuffix}"],
            cancellationToken);
    }

    public async Task<int> CountMergesAsync(
        RepositoryLocation location,
        string onto,
        CancellationToken cancellationToken)
    {
        if (onto.StartsWith('-'))
        {
            throw new ArgumentException("A rebase target cannot start with '-'", nameof(onto));
        }

        var result = await git
            .RunAsync(
                location.CommandPath,
                ["rev-list", "--count", "--merges", $"{onto}{CommitSuffix}..{GitOptions.HeadRevision}"],
                CountOptions,
                cancellationToken)
            .ConfigureAwait(false);
        return int.Parse(result.StandardOutputText.Trim(), CultureInfo.InvariantCulture);
    }
}
