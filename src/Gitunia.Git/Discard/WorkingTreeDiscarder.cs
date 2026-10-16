using System.Text;
using Gitunia.Git.Execution;
using Gitunia.Git.Repositories.Models;
using Gitunia.Platform.FileSystem;

namespace Gitunia.Git.Discard;

/// <summary>
/// Discards working-tree changes with the git CLI. Tracked paths go to git on stdin (NUL-separated, bytes exact) and are taken
/// literally; untracked paths are arguments to <c>git clean</c>, which only ever deletes paths git does not track.
/// </summary>
public sealed class WorkingTreeDiscarder(GitRunner runner, IByteExactFileSystem? fileSystem = null) : IWorkingTreeDiscarder
{
    private const string DirectorySuffix = "/";

    private readonly IByteExactFileSystem _fileSystem = fileSystem ?? ByteExactFileSystems.ForCurrentOs();

    private const string LiteralPathspecs = "--literal-pathspecs";
    private const string RestoreWorktree = "--worktree";
    private const string CleanForce = "-f";
    private const string CleanDirectories = "-d";
    private const string CleanQuiet = "-q";
    private const int PathsPerClean = 64;

    private static readonly TimeSpan WriteTimeout = TimeSpan.FromMinutes(2);

    private static readonly UTF8Encoding PatchEncoding = new(false);

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

    public bool CanDeleteInvalidNames => _fileSystem.IsSupported;

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
        if (invalid.Count > 0 && !_fileSystem.IsSupported)
        {
            throw new NotSupportedException("A name that is not valid UTF-8 cannot be deleted on this OS: delete it by hand.");
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

    // Git cannot be given the name as an argument, so the untracked files (not ignored, as with clean) are listed with their exact
    // bytes and the ones under the chosen paths are deleted by those bytes; directories left empty go afterwards.
    private async Task DeleteInvalidNamesAsync(
        RepositoryLocation location,
        List<string> targets,
        CancellationToken cancellationToken)
    {
        var listing = await runner.RunAsync(
            location.CommandPath,
            ["ls-files", "--others", "--exclude-standard", GitOptions.NulTerminated],
            new GitRunOptions(Timeout: WriteTimeout, ReadOnly: true),
            cancellationToken).ConfigureAwait(false);
        var untracked = GitPathText.Decode(listing.StandardOutput)
            .Split('\0', StringSplitOptions.RemoveEmptyEntries)
            .Where(path => !path.EndsWith(DirectorySuffix, StringComparison.Ordinal))
            .ToList();
        var root = GitPathText.Encode(location.CommandPath);
        var emptied = new SortedSet<string>(Comparer<string>.Create((a, b) => b.Length != a.Length ? b.Length.CompareTo(a.Length) : string.CompareOrdinal(a, b)));
        foreach (var target in targets)
        {
            var isDirectory = target.EndsWith(DirectorySuffix, StringComparison.Ordinal);
            foreach (var path in untracked.Where(path => isDirectory ? path.StartsWith(target, StringComparison.Ordinal) : path == target))
            {
                cancellationToken.ThrowIfCancellationRequested();
                _fileSystem.DeleteFile(Absolute(root, path));
                // Every directory from the file up to the chosen one (a file target has none).
                for (var slash = isDirectory ? path.LastIndexOf('/') : -1; slash >= target.Length - 1; slash = path.LastIndexOf('/', slash - 1))
                {
                    emptied.Add(path[..slash]);
                    if (slash == 0)
                    {
                        break;
                    }
                }
            }

            if (isDirectory)
            {
                emptied.Add(target[..^1]);
            }
        }

        foreach (var directory in emptied)
        {
            _fileSystem.TryRemoveEmptyDirectory(Absolute(root, directory));
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

    public async Task ApplyReversePatchAsync(
        RepositoryLocation location,
        string patch,
        CancellationToken cancellationToken)
    {
        var options = new GitRunOptions(Timeout: WriteTimeout, StandardInput: PatchEncoding.GetBytes(patch));
        await runner.RunAsync(location.CommandPath, ReversePatchArguments, options, cancellationToken)
            .ConfigureAwait(false);
    }
}
