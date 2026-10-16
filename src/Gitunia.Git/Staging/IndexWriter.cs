using System.Text;
using Gitunia.Git.Diff.Models;
using Gitunia.Git.Execution;
using Gitunia.Git.Repositories.Models;

namespace Gitunia.Git.Staging;

/// <summary>
/// Stages and unstages files with the git CLI. Paths go to git on stdin (NUL-separated, bytes exact) and are taken literally, so
/// names with glob characters, a leading colon or bytes that are not UTF-8 work and the argument list never grows with the selection.
/// </summary>
public sealed class IndexWriter(GitRunner runner) : IIndexWriter
{
    private const string LiteralPathspecs = "--literal-pathspecs";

    private static readonly TimeSpan WriteTimeout = TimeSpan.FromMinutes(2);

    private static readonly UTF8Encoding PatchEncoding = new(false);

    private static readonly string[] ForwardApplyArguments =
        ["apply", GitOptions.ApplyToIndex, GitOptions.ApplyNoWhitespaceWarning, GitOptions.StandardInputPath];

    private static readonly string[] ReverseApplyArguments =
        ["apply", GitOptions.ApplyToIndex, GitOptions.ApplyReverse, GitOptions.ApplyNoWhitespaceWarning, GitOptions.StandardInputPath];

    public Task StageAsync(RepositoryLocation location, IReadOnlyList<string> paths, CancellationToken cancellationToken) =>
        RunAsync(location, paths, ["add"], cancellationToken);

    public Task UnstageAsync(RepositoryLocation location, IReadOnlyList<string> paths, bool isHeadUnborn, CancellationToken cancellationToken) =>
        RunAsync(location, paths, isHeadUnborn ? ["rm", "--cached", "--force", "--quiet", "-r"] : ["restore", "--staged"], cancellationToken);

    public async Task ApplyPatchAsync(
        RepositoryLocation location,
        string patch,
        PatchDirection direction,
        CancellationToken cancellationToken)
    {
        var arguments = direction == PatchDirection.Reverse ? ReverseApplyArguments : ForwardApplyArguments;
        var options = new GitRunOptions(Timeout: WriteTimeout, StandardInput: PatchEncoding.GetBytes(patch));
        await runner.RunAsync(location.CommandPath, arguments, options, cancellationToken).ConfigureAwait(false);
    }

    private async Task RunAsync(RepositoryLocation location, IReadOnlyList<string> paths, string[] command, CancellationToken cancellationToken)
    {
        if (paths.Count == 0)
        {
            return;
        }

        string[] arguments = [LiteralPathspecs, .. command, GitOptions.PathspecFromStandardInput, GitOptions.PathspecFileNul];
        var options = new GitRunOptions(Timeout: WriteTimeout, StandardInput: GitPathText.EncodePathspecFile(paths));
        await runner.RunAsync(location.CommandPath, arguments, options, cancellationToken).ConfigureAwait(false);
    }
}
