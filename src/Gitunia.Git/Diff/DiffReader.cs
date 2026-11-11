using System.Runtime.CompilerServices;
using System.Text;
using Gitunia.Git.Diff.Models;
using Gitunia.Git.Execution;
using Gitunia.Git.Repositories.Models;
using Gitunia.Git.Status.Models;
using Gitunia.Platform.Files;

namespace Gitunia.Git.Diff;

/// <summary>Streams the files of a diff as <see cref="DiffFile"/> without buffering all of it.</summary>
public sealed class DiffReader : IDiffReader
{
    /// <summary>Flags that make the patch text predictable whatever the user's git config says.</summary>
    private static readonly string[] FixedOptions =
    [
        GitOptions.NoColor,
        "--no-ext-diff",
        "--no-textconv",
        "--src-prefix=a/",
        "--dst-prefix=b/",
        "--find-renames",
        "--find-copies",
    ];

    private readonly GitRunner _runner;

    public DiffReader(GitRunner runner)
    {
        _runner = runner;
    }

    /// <summary>Exit code of <c>git diff --no-index</c> when the files differ, which is the normal case here.</summary>
    private const int DifferencesExitCode = 1;

    private static readonly GitRunOptions NoIndexRun = GitRunOptions.ReadOnlyRun with { AcceptedExitCodes = [DifferencesExitCode] };

    public IAsyncEnumerable<DiffFile> ReadAsync(
        RepositoryLocation location,
        IReadOnlyList<string> command,
        ICollection<string>? warnings = null,
        int maximumLinesPerFile = int.MaxValue,
        CancellationToken cancellationToken = default) =>
        StreamAsync(location, command, GitRunOptions.ReadOnlyRun, false, warnings, maximumLinesPerFile, cancellationToken);

    public IAsyncEnumerable<DiffFile> ReadWorkingFileAsync(
        RepositoryLocation location,
        WorkingDiffSide side,
        StatusEntry entry,
        ICollection<string>? warnings = null,
        int maximumLinesPerFile = int.MaxValue,
        DiffOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        if (GitPathText.HasEscapedBytes(entry.Path) || (entry.OriginalPath is { } original && GitPathText.HasEscapedBytes(original)))
        {
            // Unlike add and restore, diff cannot read its paths from stdin, and such a name cannot be a process argument.
            return ReadEscapedPathAsync(location, side, entry, warnings, maximumLinesPerFile, options, cancellationToken);
        }

        var extra = (options ?? DiffOptions.Default).ToArguments();
        var isUntracked = side == WorkingDiffSide.Unstaged && entry.Kind == StatusEntryKind.Untracked;
        var isConflicted = side == WorkingDiffSide.Unstaged && entry.Kind == StatusEntryKind.Unmerged;
        IReadOnlyList<string> command;
        if (isUntracked)
        {
            command = ["diff", GitOptions.NoIndex, .. extra, GitOptions.EndOfOptions, GitOptions.NullDevice, entry.Path];
        }
        else if (side == WorkingDiffSide.Staged)
        {
            command = ["diff", GitOptions.Cached, .. extra, GitOptions.EndOfOptions, .. LiteralPaths(entry, includeOriginal: true)];
        }
        else
        {
            command = ["diff", .. extra, GitOptions.EndOfOptions, .. LiteralPaths(entry, includeOriginal: false)];
        }

        var run = isUntracked ? NoIndexRun : GitRunOptions.ReadOnlyRun;
        return StreamAsync(location, command, run, isConflicted, warnings, maximumLinesPerFile, cancellationToken);
    }

    /// <summary>
    /// Reads the diff of a file whose name cannot be an argument: an untracked file is read in-process and written as additions;
    /// for a tracked one the unfiltered diff is streamed and the file is picked by its lossily decoded path, and the stream
    /// stops after it (disposing it kills git).
    /// </summary>
    private async IAsyncEnumerable<DiffFile> ReadEscapedPathAsync(
        RepositoryLocation location,
        WorkingDiffSide side,
        StatusEntry entry,
        ICollection<string>? warnings,
        int maximumLinesPerFile,
        DiffOptions? options,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var nameBytes = GitPathText.Encode(entry.Path);
        if (side == WorkingDiffSide.Unstaged && entry.Kind == StatusEntryKind.Untracked)
        {
            var parser = new DiffParser(maximumLinesPerFile);
            var files = new List<DiffFile>();
            var content = await Task.Run(() => ReadRawFile(location, nameBytes), cancellationToken).ConfigureAwait(false);
            if (content is not null)
            {
                parser.Feed(NewFilePatch.Write(nameBytes, content), files);
                parser.Complete(files);
            }

            foreach (var file in files)
            {
                yield return file;
            }

            yield break;
        }

        var lossyPath = Encoding.UTF8.GetString(nameBytes);
        var isConflicted = side == WorkingDiffSide.Unstaged && entry.Kind == StatusEntryKind.Unmerged;
        var extra = (options ?? DiffOptions.Default).ToArguments();
        IReadOnlyList<string> command = side == WorkingDiffSide.Staged ? ["diff", GitOptions.Cached, .. extra] : ["diff", .. extra];
        await foreach (var file in StreamAsync(
            location,
            command,
            GitRunOptions.ReadOnlyRun,
            isConflicted,
            warnings,
            maximumLinesPerFile,
            cancellationToken).ConfigureAwait(false))
        {
            if (file.Path == lossyPath)
            {
                yield return file;
                yield break;
            }
        }
    }

    private static byte[]? ReadRawFile(RepositoryLocation location, byte[] nameBytes)
    {
        if (location.WorkingTreePath is not { } root)
        {
            return null;
        }

        var prefix = Encoding.UTF8.GetBytes(Path.TrimEndingDirectorySeparator(root) + Path.DirectorySeparatorChar);

        return RawPathFileReader.TryReadAll([.. prefix, .. nameBytes]);
    }

    private static IEnumerable<string> LiteralPaths(StatusEntry entry, bool includeOriginal)
    {
        yield return GitOptions.LiteralPathspecPrefix + entry.Path;
        if (includeOriginal && entry.OriginalPath is { } original)
        {
            // Both sides of a rename, or git shows an addition and no rename.
            yield return GitOptions.LiteralPathspecPrefix + original;
        }
    }

    private async IAsyncEnumerable<DiffFile> StreamAsync(
        RepositoryLocation location,
        IReadOnlyList<string> command,
        GitRunOptions run,
        bool readsCombined,
        ICollection<string>? warnings,
        int maximumLinesPerFile,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfZero(command.Count);
        var arguments = new List<string>(command.Count + FixedOptions.Length) { command[0] };
        arguments.AddRange(FixedOptions);
        for (var index = 1; index < command.Count; index++)
        {
            arguments.Add(command[index]);
        }

        var parser = new DiffParser(maximumLinesPerFile, readsCombined);
        var completed = new List<DiffFile>();
        try
        {
            await foreach (var chunk in _runner
                .StreamAsync(location.CommandPath, arguments, run, cancellationToken)
                .ConfigureAwait(false))
            {
                parser.Feed(chunk.Span, completed);
                foreach (var file in completed)
                {
                    yield return file;
                }

                completed.Clear();
            }

            parser.Complete(completed);
            foreach (var file in completed)
            {
                yield return file;
            }
        }
        finally
        {
            // Also runs when the caller stops reading early, so warnings seen so far are not lost.
            if (warnings is not null)
            {
                foreach (var warning in parser.Warnings)
                {
                    warnings.Add(warning);
                }
            }
        }
    }

    public IAsyncEnumerable<DiffFile> ReadCommitAsync(
        RepositoryLocation location,
        ObjectId commit,
        ObjectId? firstParent,
        ICollection<string>? warnings = null,
        int maximumLinesPerFile = int.MaxValue,
        DiffOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var extra = (options ?? DiffOptions.Default).ToArguments();
        if (firstParent is { } parent)
        {
            string[] diffCommand = ["diff", .. extra, parent.ToString(), commit.ToString()];

            return ReadAsync(location, diffCommand, warnings, maximumLinesPerFile, cancellationToken);
        }

        string[] showCommand = ["show", "--format=", .. extra, commit.ToString()];

        return ReadAsync(location, showCommand, warnings, maximumLinesPerFile, cancellationToken);
    }

    public IAsyncEnumerable<DiffFile> ReadCommitAgainstFirstParentAsync(
        RepositoryLocation location,
        ObjectId commit,
        ICollection<string>? warnings = null,
        int maximumLinesPerFile = int.MaxValue,
        DiffOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var extra = (options ?? DiffOptions.Default).ToArguments();
        string[] command = ["show", "--format=", GitOptions.FirstParent, .. extra, commit.ToString()];

        return ReadAsync(location, command, warnings, maximumLinesPerFile, cancellationToken);
    }
}
