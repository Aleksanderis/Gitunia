using Gitunia.Git;
using Gitunia.Git.Commits;
using Gitunia.Git.Commits.Models;
using Gitunia.Git.Execution.Exceptions;
using Gitunia.Git.Repositories.Models;

namespace Gitunia.Tui.Tests.Fakes;

/// <summary>Serves commit details from memory, so tests of the code on top of the reader need no git repository. An id that was not added fails like git does for an unknown commit.</summary>
public sealed class FakeCommitDetailsReader : ICommitDetailsReader
{
    private static readonly DateTimeOffset CommitTime = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private readonly Dictionary<ObjectId, CommitDetails> _commits = [];
    private readonly Dictionary<(ObjectId Commit, ObjectId Parent), IReadOnlyList<CommitFileChange>> _files = [];

    /// <summary>A location that is never touched on disk; the fake ignores it.</summary>
    public static RepositoryLocation Location { get; } = new(
        Path.Combine(Path.GetTempPath(), "gitunia-fake-repository"),
        Path.Combine(Path.GetTempPath(), "gitunia-fake-repository", ".git"),
        Path.Combine(Path.GetTempPath(), "gitunia-fake-repository", ".git"),
        false,
        false);

    /// <summary>The ids made by <see cref="Chain"/>, newest first (index 0 is the tip, index n is its n-th ancestor).</summary>
    public IReadOnlyList<ObjectId> Commits { get; private set; } = [];

    /// <summary>The number of <see cref="ReadFilesAsync"/> calls so far.</summary>
    public int ReadFilesCount { get; private set; }

    /// <summary>The number of reads so far.</summary>
    public int ReadCount { get; private set; }

    /// <summary>A linear history of <paramref name="count"/> commits named "commit 1" (oldest) to "commit N" (tip).</summary>
    public static FakeCommitDetailsReader Chain(int count)
    {
        var reader = new FakeCommitDetailsReader();
        var ids = new List<ObjectId>(count);
        for (var number = 1; number <= count; number++)
        {
            ids.Add(reader.Add($"commit {number}", ids.Count == 0 ? null : ids[^1]));
        }

        ids.Reverse();
        reader.Commits = ids;
        return reader;
    }

    /// <summary>Adds a commit with this subject and an optional parent; returns its new id.</summary>
    public ObjectId Add(string subject, ObjectId? parent = null)
    {
        var id = FakeObjectIds.Of(_commits.Count + 1);
        IReadOnlyList<ObjectId> parents = parent is { } known ? [known] : [];
        _commits[id] = new CommitDetails(id, parents, "Author", "author@example.com", CommitTime, "Author", "author@example.com", CommitTime, subject, []);
        return id;
    }

    /// <summary>Adds a merge commit of these parents (first parent first); returns its new id.</summary>
    public ObjectId AddMerge(string subject, params ObjectId[] parents)
    {
        var id = FakeObjectIds.Of(_commits.Count + 1);
        _commits[id] = new CommitDetails(
            id,
            parents,
            "Author",
            "author@example.com",
            CommitTime,
            "Author",
            "author@example.com",
            CommitTime,
            subject,
            []);
        return id;
    }

    /// <summary>Sets the files <see cref="ReadFilesAsync"/> returns for this commit against this parent.</summary>
    public void SetFiles(ObjectId commit, ObjectId parent, params string[] paths) =>
        _files[(commit, parent)] =
            [.. paths.Select(path => new CommitFileChange(path, null, FileChangeKind.Modified, null))];

    /// <summary>An id that no added commit has.</summary>
    public static ObjectId MissingId() => FakeObjectIds.Of(int.MaxValue);

    public Task<CommitDetails?> ReadAsync(RepositoryLocation location, ObjectId id, ICollection<string>? warnings = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ReadCount++;
        return _commits.TryGetValue(id, out var details)
            ? Task.FromResult<CommitDetails?>(details)
            : throw new GitException($"unknown commit {id}");
    }

    public Task<IReadOnlyList<CommitFileChange>> ReadFilesAsync(
        RepositoryLocation location,
        ObjectId id,
        ObjectId? parent,
        ICollection<string>? warnings = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ReadFilesCount++;
        return Task.FromResult(parent is { } known && _files.TryGetValue((id, known), out var files) ? files : []);
    }
}
