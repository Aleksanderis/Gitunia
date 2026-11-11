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
        var collected = warnings ?? [];
        var result = await _runner.RunAsync(location.CommandPath, CommitDetailsFormat.CombinedArguments(id), GitRunOptions.ReadOnlyRun, cancellationToken).ConfigureAwait(false);
        var output = result.StandardOutput.AsSpan();
        var details = CommitDetailsParser.Parse(output, collected);
        if (details is null)
        {
            return null;
        }

        // The raw entries follow the record's NUL; the first of them starts with a newline.
        var terminator = output.IndexOf((byte)0);
        var rest = terminator < 0 ? default : output[(terminator + 1)..];
        if (rest.Length > 0 && rest[0] == (byte)'\n')
        {
            rest = rest[1..];
        }

        var changes = CommitFileChangeParser.Parse(rest);
        foreach (var warning in changes.Warnings)
        {
            collected.Add(warning);
        }

        return details with { Files = changes.Items };
    }

    public async Task<IReadOnlyList<CommitFileChange>> ReadFilesAsync(
        RepositoryLocation location,
        ObjectId id,
        ObjectId? parent,
        ICollection<string>? warnings = null,
        CancellationToken cancellationToken = default)
    {
        var files = await _runner.RunAsync(
            location.CommandPath,
            CommitDetailsFormat.FileArguments(id, parent),
            GitRunOptions.ReadOnlyRun,
            cancellationToken).ConfigureAwait(false);
        var changes = CommitFileChangeParser.Parse(files.StandardOutput);
        foreach (var warning in changes.Warnings)
        {
            warnings?.Add(warning);
        }

        return changes.Items;
    }
}
