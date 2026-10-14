using Gitunia.Git.Commits.Models;
using Gitunia.Git.Execution;
using Gitunia.Git.Repositories.Models;

namespace Gitunia.Git.Commits;

/// <summary>Reads the message, people, parents and changed files of one commit.</summary>
public sealed class CommitDetailsReader : ICommitDetailsReader
{
    private readonly GitRunner _runner;

    public CommitDetailsReader(GitRunner runner)
    {
        _runner = runner;
    }

    public async Task<CommitDetails?> ReadAsync(RepositoryLocation location, ObjectId id, ICollection<string>? warnings = null, CancellationToken cancellationToken = default)
    {
        var collected = warnings ?? new List<string>();
        var metadata = await _runner.RunAsync(location.CommandPath, CommitDetailsFormat.MetadataArguments(id), GitRunOptions.ReadOnlyRun, cancellationToken).ConfigureAwait(false);
        var details = CommitDetailsParser.Parse(metadata.StandardOutput, collected);
        if (details is null)
        {
            return null;
        }

        var firstParent = details.Parents.Count > 0 ? details.Parents[0] : (ObjectId?)null;
        var files = await _runner.RunAsync(location.CommandPath, CommitDetailsFormat.FileArguments(id, firstParent), GitRunOptions.ReadOnlyRun, cancellationToken).ConfigureAwait(false);
        var changes = CommitFileChangeParser.Parse(files.StandardOutput);
        foreach (var warning in changes.Warnings)
        {
            collected.Add(warning);
        }

        return details with { Files = changes.Items };
    }
}
