namespace Gitunia.Git;

/// <summary>Reads the message, people, parents and changed files of one commit.</summary>
public static class CommitDetailsReader
{
    /// <param name="repo">The repository.</param>
    /// <param name="id">The commit to read.</param>
    /// <param name="cancellationToken">Cancels both git processes.</param>
    /// <param name="warnings">Receives the parsers' warnings.</param>
    /// <returns>The details; null when git printed no commit.</returns>
    public static async Task<CommitDetails?> ReadAsync(Repo repo, ObjectId id, CancellationToken cancellationToken, ICollection<string>? warnings = null)
    {
        var options = new GitRunOptions(ReadOnly: true);
        var collected = warnings ?? new List<string>();
        var metadata = await repo.RunAsync(CommitDetailsFormat.MetadataArguments(id), options, cancellationToken).ConfigureAwait(false);
        var details = CommitDetailsParser.Parse(metadata.StandardOutput, collected);
        if (details is null)
        {
            return null;
        }

        var firstParent = details.Parents.Count > 0 ? details.Parents[0] : (ObjectId?)null;
        var files = await repo.RunAsync(CommitDetailsFormat.FileArguments(id, firstParent), options, cancellationToken).ConfigureAwait(false);
        var changes = CommitFileChangeParser.Parse(files.StandardOutput);
        foreach (var warning in changes.Warnings)
        {
            collected.Add(warning);
        }

        return details with { Files = changes.Items };
    }
}
