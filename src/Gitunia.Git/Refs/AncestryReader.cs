using Gitunia.Git.Execution;
using Gitunia.Git.Repositories.Models;

namespace Gitunia.Git.Refs;

/// <summary>Ancestry checks with <c>merge-base --is-ancestor</c> (exit 0 yes, exit 1 no).</summary>
public sealed class AncestryReader(GitRunner runner) : IAncestryReader
{
    private const string MergeBase = "merge-base";
    private const string IsAncestor = "--is-ancestor";

    private static readonly GitRunOptions Options = new(
        ReadOnly: true,
        AcceptedExitCodes: [GitRunOptions.NoMatchExitCode]);

    public async Task<bool> IsAncestorAsync(
        RepositoryLocation location,
        string ancestor,
        string descendant,
        CancellationToken cancellationToken)
    {
        var result = await runner
            .RunAsync(location.CommandPath, [MergeBase, IsAncestor, ancestor, descendant], Options, cancellationToken)
            .ConfigureAwait(false);
        return result.ExitCode == 0;
    }
}
