using Gitunia.Git.Execution;
using Gitunia.Git.Repositories.Models;
using Gitunia.Git.Trees.Models;

namespace Gitunia.Git.Trees;

/// <summary>Lists the tree of a commit with <c>git ls-tree -r -z --long</c>.</summary>
public sealed class CommitTreeReader(GitRunner runner) : ICommitTreeReader
{
    public async Task<IReadOnlyList<TreeEntry>> ReadAsync(
        RepositoryLocation location,
        ObjectId commit,
        ICollection<string>? warnings = null,
        CancellationToken cancellationToken = default)
    {
        var result = await runner
            .RunAsync(
                location.CommandPath,
                CommitTreeFormat.Arguments(commit),
                GitRunOptions.ReadOnlyRun,
                cancellationToken)
            .ConfigureAwait(false);
        var parsed = CommitTreeParser.Parse(result.StandardOutput);
        foreach (var warning in parsed.Warnings)
        {
            warnings?.Add(warning);
        }

        return parsed.Items;
    }
}
