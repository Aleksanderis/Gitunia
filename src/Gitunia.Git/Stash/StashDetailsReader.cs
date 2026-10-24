using System.Runtime.CompilerServices;
using Gitunia.Git.Commits;
using Gitunia.Git.Commits.Models;
using Gitunia.Git.Diff;
using Gitunia.Git.Diff.Models;
using Gitunia.Git.Execution;
using Gitunia.Git.Repositories.Models;
using Gitunia.Git.Stash.Models;

namespace Gitunia.Git.Stash;

/// <summary>Reads the files and diff of a stash by reusing the commit file parser and the diff reader.</summary>
public sealed class StashDetailsReader : IStashDetailsReader
{
    /// <summary>Placed after <c>--find-copies</c> so it wins; <c>git stash show</c> finds renames only.</summary>
    private const string RenamesOnlyOption = "--find-renames";

    private const int BaseParentField = 1;
    private const int UntrackedParentField = 3;

    private static readonly string[] ListArguments = ["stash", "list", $"--format={StashParser.Format}"];

    private static readonly string[] ParentsArguments = ["rev-list", "--parents", "-n", "1"];

    private static readonly char[] FieldSeparators = [' ', '\n'];

    private readonly GitRunner _runner;
    private readonly IDiffReader _diffs;

    public StashDetailsReader(GitRunner runner)
        : this(runner, new DiffReader(runner))
    {
    }

    public StashDetailsReader(GitRunner runner, IDiffReader diffs)
    {
        _runner = runner;
        _diffs = diffs;
    }

    public async Task<StashFiles?> ReadFilesAsync(
        RepositoryLocation location,
        StashEntry entry,
        ICollection<string>? warnings = null,
        CancellationToken cancellationToken = default)
    {
        var list = await _runner
            .RunAsync(location.CommandPath, ListArguments, GitRunOptions.ReadOnlyRun, cancellationToken)
            .ConfigureAwait(false);
        if (StashParser.Parse(list.StandardOutputText).Items.All(candidate => candidate.CommitHash != entry.CommitHash))
        {
            return null;
        }

        var parents = await ReadParentsAsync(location, entry, cancellationToken).ConfigureAwait(false);
        var files = new List<StashFileChange>();
        if (parents.Base is { } baseCommit && ObjectId.TryParse(entry.CommitHash, out var stash))
        {
            var tracked = await ReadChangesAsync(location, stash, baseCommit, warnings, cancellationToken)
                .ConfigureAwait(false);
            files.AddRange(tracked.Select(change => new StashFileChange(change, StashFileOrigin.Tracked)));
        }

        if (parents.Untracked is { } untracked)
        {
            var added = await ReadChangesAsync(location, untracked, null, warnings, cancellationToken)
                .ConfigureAwait(false);
            files.AddRange(added.Select(change => new StashFileChange(change, StashFileOrigin.Untracked)));
        }

        return new StashFiles(entry.CommitHash, files);
    }

    public async IAsyncEnumerable<DiffFile> ReadDiffAsync(
        RepositoryLocation location,
        StashEntry entry,
        StashFileOrigin origin,
        ICollection<string>? warnings = null,
        int maximumLinesPerFile = int.MaxValue,
        DiffOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var parents = await ReadParentsAsync(location, entry, cancellationToken).ConfigureAwait(false);
        ObjectId? commit;
        ObjectId? parent = null;
        if (origin == StashFileOrigin.Tracked)
        {
            commit = ObjectId.TryParse(entry.CommitHash, out var stash) ? stash : null;
            parent = parents.Base;
            if (parent is null)
            {
                yield break;
            }
        }
        else
        {
            commit = parents.Untracked;
        }

        if (commit is not { } target)
        {
            yield break;
        }

        var extra = (options ?? DiffOptions.Default).ToArguments();
        IReadOnlyList<string> command = parent is { } baseCommit
            ? ["diff", .. extra, RenamesOnlyOption, baseCommit.ToString(), target.ToString()]
            : ["show", "--format=", .. extra, target.ToString()];

        await foreach (var file in _diffs
            .ReadAsync(location, command, warnings, maximumLinesPerFile, cancellationToken)
            .ConfigureAwait(false))
        {
            yield return file;
        }
    }

    private async Task<IReadOnlyList<CommitFileChange>> ReadChangesAsync(
        RepositoryLocation location,
        ObjectId commit,
        ObjectId? parent,
        ICollection<string>? warnings,
        CancellationToken cancellationToken)
    {
        var result = await _runner
            .RunAsync(
                location.CommandPath,
                CommitDetailsFormat.RenameOnlyFileArguments(commit, parent),
                GitRunOptions.ReadOnlyRun,
                cancellationToken)
            .ConfigureAwait(false);
        var changes = CommitFileChangeParser.Parse(result.StandardOutput);
        foreach (var warning in changes.Warnings)
        {
            warnings?.Add(warning);
        }

        return changes.Items;
    }

    private async Task<StashParents> ReadParentsAsync(
        RepositoryLocation location,
        StashEntry entry,
        CancellationToken cancellationToken)
    {
        string[] arguments = [.. ParentsArguments, entry.CommitHash, GitOptions.EndOfOptions];
        var result = await _runner
            .RunAsync(location.CommandPath, arguments, GitRunOptions.ReadOnlyRun, cancellationToken)
            .ConfigureAwait(false);

        // "<stash> <base> <index> [<untracked>]": the third parent exists only for --include-untracked.
        var fields = result.StandardOutputText.Split(FieldSeparators, StringSplitOptions.RemoveEmptyEntries);
        return new StashParents(ParentAt(fields, BaseParentField), ParentAt(fields, UntrackedParentField));
    }

    private static ObjectId? ParentAt(string[] fields, int index) =>
        fields.Length > index && ObjectId.TryParse(fields[index], out var id) ? id : null;

    private sealed record StashParents(ObjectId? Base, ObjectId? Untracked);
}
