using System.Globalization;
using System.Runtime.CompilerServices;
using Gitunia.Git;
using Gitunia.Git.Diff;
using Gitunia.Git.Diff.Models;
using Gitunia.Git.Execution.Exceptions;
using Gitunia.Git.Repositories.Models;
using Gitunia.Git.Status.Models;

namespace Gitunia.Tui.Tests.Fakes;

/// <summary>Serves diffs from memory, so tests of the diff store need no git repository. An id that was not added fails like git does for an unknown commit.</summary>
public sealed class FakeDiffReader : IDiffReader
{
    private const int HexDigitCount = 40;

    private readonly Dictionary<ObjectId, FakeCommit> _commits = [];
    private readonly Dictionary<(WorkingDiffSide Side, string Path), DiffFile[]> _working = [];

    /// <summary>The number of commit diffs started so far.</summary>
    public int ReadCount { get; private set; }

    /// <summary>The number of working-tree file diffs started so far.</summary>
    public int WorkingReadCount { get; private set; }

    /// <summary>The files a working-tree read of <paramref name="path"/> on <paramref name="side"/> returns from now on.</summary>
    public void SetWorking(WorkingDiffSide side, string path, params DiffFile[] files) => _working[(side, path)] = files;

    /// <summary>A file with one hunk that adds these lines.</summary>
    public static DiffFile FileAdding(string path, IEnumerable<string> lines)
    {
        var added = new List<DiffLine>();
        foreach (var line in lines)
        {
            added.Add(new DiffLine(DiffLineKind.Added, line, null, added.Count + 1, false));
        }

        var hunk = new DiffHunk($"@@ -0,0 +1,{added.Count} @@", 0, 0, 1, added.Count, string.Empty, added);
        return new DiffFile(null, path, DiffFileStatus.Added, null, null, null, false, [], [hunk]);
    }

    /// <summary>A file with one hunk that adds <paramref name="lineCount"/> lines.</summary>
    public static DiffFile FileAdding(string path, int lineCount) =>
        FileAdding(path, Enumerable.Range(0, lineCount).Select(index => $"line {index.ToString(CultureInfo.InvariantCulture)}"));

    /// <summary>An id that no added commit has.</summary>
    public static ObjectId MissingId() => IdOf(int.MaxValue);

    /// <summary>Adds a commit that changes these files against this parent; returns its new id.</summary>
    public ObjectId Add(ObjectId? parent, params DiffFile[] files)
    {
        var id = IdOf(_commits.Count + 1);
        _commits[id] = new FakeCommit(parent, files);
        return id;
    }

    public async IAsyncEnumerable<DiffFile> ReadAsync(
        RepositoryLocation location,
        IReadOnlyList<string> command,
        ICollection<string>? warnings = null,
        int maximumLinesPerFile = int.MaxValue,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;
        cancellationToken.ThrowIfCancellationRequested();
        yield break;
    }

    public async IAsyncEnumerable<DiffFile> ReadCommitAsync(
        RepositoryLocation location,
        ObjectId commit,
        ObjectId? firstParent,
        ICollection<string>? warnings = null,
        int maximumLinesPerFile = int.MaxValue,
        DiffOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await Task.Yield();
        cancellationToken.ThrowIfCancellationRequested();
        ReadCount++;
        if (!_commits.TryGetValue(commit, out var known))
        {
            throw new GitException($"unknown commit {commit}");
        }

        foreach (var file in known.Files)
        {
            yield return file;
        }
    }

    public async IAsyncEnumerable<DiffFile> ReadWorkingFileAsync(
        RepositoryLocation location,
        WorkingDiffSide side,
        StatusEntry entry,
        ICollection<string>? warnings = null,
        int maximumLinesPerFile = int.MaxValue,
        DiffOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await Task.Yield();
        cancellationToken.ThrowIfCancellationRequested();
        WorkingReadCount++;
        if (!_working.TryGetValue((side, entry.Path), out var files))
        {
            throw new GitException($"no diff for {entry.Path}");
        }

        foreach (var file in files)
        {
            yield return file;
        }
    }

    public Task<ObjectId?> ReadFirstParentAsync(RepositoryLocation location, ObjectId commit, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return _commits.TryGetValue(commit, out var known)
            ? Task.FromResult(known.Parent)
            : throw new GitException($"unknown commit {commit}");
    }

    private static ObjectId IdOf(int number)
    {
        var text = number.ToString("x", CultureInfo.InvariantCulture).PadLeft(HexDigitCount, '0');
        return ObjectId.TryParse(text, out var id) ? id : throw new InvalidOperationException(text);
    }

    private sealed record FakeCommit(ObjectId? Parent, IReadOnlyList<DiffFile> Files);
}
