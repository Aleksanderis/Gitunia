using System.Text;
using Gitunia.Git.Execution;
using Gitunia.Git.Repositories.Models;

namespace Gitunia.Git.Discard;

/// <summary>
/// Discards working-tree changes with the git CLI. Tracked paths go to git on stdin (NUL-separated, bytes exact) and are taken
/// literally; untracked paths are arguments to <c>git clean</c>, which only ever deletes paths git does not track.
/// </summary>
public sealed class WorkingTreeDiscarder(GitRunner runner) : IWorkingTreeDiscarder
{
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
        if (paths.Any(GitPathText.HasEscapedBytes))
        {
            throw new NotSupportedException("A name that is not valid UTF-8 cannot be deleted from here: delete it by hand.");
        }

        var options = new GitRunOptions(Timeout: WriteTimeout);
        foreach (var batch in paths.Chunk(PathsPerClean))
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
}
