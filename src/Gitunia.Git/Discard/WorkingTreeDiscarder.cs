using System.Text;
using Gitunia.Git.Execution;
using Gitunia.Git.Repositories.Models;
using Gitunia.Platform.FileSystem;

namespace Gitunia.Git.Discard;

/// <summary>
/// Discards working-tree changes with the git CLI. Tracked paths go to git on stdin (NUL-separated, bytes exact) and are taken
/// literally; untracked paths are arguments to <c>git clean</c>, which only ever deletes paths git does not track.
/// </summary>
public sealed class WorkingTreeDiscarder(GitRunner runner, IByteExactFileSystem fileSystem) : IWorkingTreeDiscarder
{
    private const string DirectorySuffix = "/";
    private const char DirectorySeparator = '/';
    private const char NulSeparator = '\0';
    private const string LiteralPathspecs = "--literal-pathspecs";
    private const string RestoreWorktree = "--worktree";
    private const string CleanForce = "-f";
    private const string CleanDirectories = "-d";
    private const string CleanQuiet = "-q";
    private const int PathsPerClean = 64;

    private static readonly TimeSpan WriteTimeout = TimeSpan.FromMinutes(2);

    private static readonly UTF8Encoding PatchEncoding = new(false);

    private static readonly string[] ListUntrackedArguments =
        ["ls-files", "--others", "--exclude-standard", GitOptions.NulTerminated];

    private static readonly string[] RestoreArguments =
    [
        LiteralPathspecs,
        "restore",
        RestoreWorktree,
        GitOptions.PathspecFromStandardInput,
        GitOptions.PathspecFileNul,
    ];

    private static readonly string[] ReversePatchArguments =
        ["apply", GitOptions.ApplyReverse, GitOptions.ApplyNoWhitespaceWarning, GitOptions.StandardInputPath];

    public bool CanDeleteInvalidNames => fileSystem.IsSupported;

    public async Task RestoreAsync(
        RepositoryLocation location,
        IReadOnlyList<string> paths,
        CancellationToken cancellationToken)
    {
        if (paths.Count == 0)
        {
            return;
        }

        var options = new GitRunOptions(Timeout: WriteTimeout, StandardInput: GitPathText.EncodePathspecFile(paths));
        await runner.RunAsync(location.CommandPath, RestoreArguments, options, cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteUntrackedAsync(
        RepositoryLocation location,
        IReadOnlyList<string> paths,
        CancellationToken cancellationToken)
    {
        var invalid = paths.Where(GitPathText.HasEscapedBytes).ToList();
        if (invalid.Count > 0 && !fileSystem.IsSupported)
        {
            throw new NotSupportedException(
                "A name that is not valid UTF-8 cannot be deleted on this OS: delete it by hand.");
        }

        var options = new GitRunOptions(Timeout: WriteTimeout);
        if (invalid.Count > 0)
        {
            await DeleteInvalidNamesAsync(location, invalid, cancellationToken).ConfigureAwait(false);
        }

        foreach (var batch in paths.Where(path => !GitPathText.HasEscapedBytes(path)).Chunk(PathsPerClean))
        {
            string[] arguments =
            [
                LiteralPathspecs,
                "clean",
                CleanForce,
                CleanDirectories,
                CleanQuiet,
                GitOptions.EndOfOptions,
                .. batch,
            ];
            await runner.RunAsync(location.CommandPath, arguments, options, cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task ApplyReversePatchAsync(
        RepositoryLocation location,
        string patch,
        CancellationToken cancellationToken)
    {
        var options = new GitRunOptions(Timeout: WriteTimeout, StandardInput: PatchEncoding.GetBytes(patch));
        await runner.RunAsync(location.CommandPath, ReversePatchArguments, options, cancellationToken)
            .ConfigureAwait(false);
    }

    // Git cannot be given the name as an argument, so the untracked files (not ignored, as with clean) are listed with their
    // exact bytes and the ones under the chosen paths are deleted by those bytes; directories left empty go afterwards.
    private async Task DeleteInvalidNamesAsync(
        RepositoryLocation location,
        List<string> targets,
        CancellationToken cancellationToken)
    {
        var options = new GitRunOptions(Timeout: WriteTimeout, ReadOnly: true);
        var listing = await runner.RunAsync(location.CommandPath, ListUntrackedArguments, options, cancellationToken)
            .ConfigureAwait(false);
        var untracked = GitPathText.Decode(listing.StandardOutput)
            .Split(NulSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Where(path => !path.EndsWith(DirectorySuffix, StringComparison.Ordinal))
            .ToList();
        var root = GitPathText.Encode(location.CommandPath);
        var emptied = new SortedSet<string>(Comparer<string>.Create(DeepestFirst));
        foreach (var target in targets)
        {
            var isDirectory = target.EndsWith(DirectorySuffix, StringComparison.Ordinal);
            foreach (var path in untracked.Where(path => IsUnder(path, target, isDirectory)))
            {
                cancellationToken.ThrowIfCancellationRequested();
                fileSystem.DeleteFile(Absolute(root, path));
                if (isDirectory)
                {
                    AddDirectoriesUpTo(emptied, path, target);
                }
            }

            if (isDirectory)
            {
                emptied.Add(target[..^1]);
            }
        }

        foreach (var directory in emptied)
        {
            fileSystem.TryRemoveEmptyDirectory(Absolute(root, directory));
        }
    }

    private static bool IsUnder(string path, string target, bool isDirectory) =>
        isDirectory ? path.StartsWith(target, StringComparison.Ordinal) : path == target;

    private static int DeepestFirst(string left, string right) =>
        left.Length != right.Length ? right.Length.CompareTo(left.Length) : string.CompareOrdinal(left, right);

    // Every directory from the file up to the chosen one (the target without its trailing slash).
    private static void AddDirectoriesUpTo(SortedSet<string> directories, string path, string target)
    {
        var slash = path.LastIndexOf(DirectorySeparator);
        while (slash >= target.Length - 1)
        {
            directories.Add(path[..slash]);
            if (slash == 0)
            {
                break;
            }

            slash = path.LastIndexOf(DirectorySeparator, slash - 1);
        }
    }

    private static byte[] Absolute(byte[] root, string relative)
    {
        var tail = GitPathText.Encode(relative);
        var bytes = new byte[root.Length + 1 + tail.Length];
        root.CopyTo(bytes, 0);
        bytes[root.Length] = (byte)Path.DirectorySeparatorChar;
        tail.CopyTo(bytes, root.Length + 1);
        return bytes;
    }
}
