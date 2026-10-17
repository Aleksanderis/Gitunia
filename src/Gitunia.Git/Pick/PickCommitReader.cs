using Gitunia.Git.Execution;
using Gitunia.Git.Repositories.Models;

namespace Gitunia.Git.Pick;

/// <summary>
/// Reads commits for the pick flow with <c>log --no-walk=unsorted</c>, which keeps the order of the arguments. One call
/// reads all commits; a second call reads the parents of the merge commits among them.
/// </summary>
public sealed class PickCommitReader(GitRunner runner) : IPickCommitReader
{
    private const char FieldSeparator = '\u001f';
    private const string Format = "--format=%H%x1f%P%x1f%s%x1e";
    private const string NoWalk = "--no-walk=unsorted";

    public async Task<IReadOnlyList<PickCommit>> ReadAsync(
        RepositoryLocation location,
        IReadOnlyList<string> hashes,
        CancellationToken cancellationToken)
    {
        var commits = await ReadRecordsAsync(location, hashes, cancellationToken).ConfigureAwait(false);
        var parentHashes = commits
            .Where(commit => commit.Parents.Length > 1)
            .SelectMany(commit => commit.Parents)
            .Distinct()
            .ToList();
        var subjects = parentHashes.Count == 0
            ? []
            : (await ReadRecordsAsync(location, parentHashes, cancellationToken).ConfigureAwait(false))
                .ToDictionary(record => record.Hash, record => record.Subject);

        return commits.Select(commit => ToCommit(commit, subjects)).ToList();
    }

    private static PickCommit ToCommit(Record record, Dictionary<string, string> subjects)
    {
        List<PickParent> parents = record.Parents.Length > 1
            ? [.. record.Parents.Select(parent => ParentOf(parent, subjects))]
            : [];
        return new PickCommit(record.Hash, record.Subject, parents);
    }

    private static PickParent ParentOf(string hash, Dictionary<string, string> subjects) =>
        new(hash, subjects.GetValueOrDefault(hash, string.Empty));

    private static List<Record> Parse(string output)
    {
        var records = new List<Record>();
        foreach (var text in output.Split(GitSeparators.RecordSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var fields = text.Trim('\n').Split(FieldSeparator);
            if (fields.Length == 3)
            {
                var parents = fields[1].Split(' ', StringSplitOptions.RemoveEmptyEntries);
                records.Add(new Record(fields[0], parents, fields[2]));
            }
        }

        return records;
    }

    private async Task<List<Record>> ReadRecordsAsync(
        RepositoryLocation location,
        IReadOnlyList<string> hashes,
        CancellationToken cancellationToken)
    {
        List<string> arguments = ["log", NoWalk, Format, .. hashes, GitOptions.EndOfOptions];
        var result = await runner
            .RunAsync(location.CommandPath, arguments, GitRunOptions.ReadOnlyRun, cancellationToken)
            .ConfigureAwait(false);
        return Parse(result.StandardOutputText);
    }

    private sealed record Record(string Hash, string[] Parents, string Subject);
}
