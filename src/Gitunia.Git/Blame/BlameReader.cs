using System.Runtime.CompilerServices;
using Gitunia.Git.Blame.Models;
using Gitunia.Git.Execution;
using Gitunia.Git.Repositories.Models;

namespace Gitunia.Git.Blame;

/// <summary>Blames with <c>git blame --incremental</c> and reads the lines with <c>cat-file</c> or from disk.</summary>
public sealed class BlameReader(GitRunner runner) : IBlameReader
{
    private const string Blame = "blame";
    private const string Incremental = "--incremental";
    private const string CatFile = "cat-file";
    private const string BlobType = "blob";
    private const char RevisionPathSeparator = ':';

    public async Task<BlameContent> ReadContentAsync(
        RepositoryLocation location,
        string path,
        string? revision,
        CancellationToken cancellationToken)
    {
        if (revision is null)
        {
            return await ReadWorkingTreeAsync(location, path, cancellationToken).ConfigureAwait(false);
        }

        var result = await runner
            .RunAsync(
                location.CommandPath,
                [CatFile, BlobType, $"{revision}{RevisionPathSeparator}{path}"],
                GitRunOptions.ReadOnlyRun,
                cancellationToken)
            .ConfigureAwait(false);

        return BlameTextDecoder.Decode(result.StandardOutput);
    }

    public async IAsyncEnumerable<BlameChunk> BlameAsync(
        RepositoryLocation location,
        string path,
        string? revision,
        ICollection<string>? warnings = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        List<string> arguments = [Blame, Incremental];
        if (revision is not null)
        {
            arguments.Add(revision);
        }

        arguments.Add(GitOptions.EndOfOptions);
        arguments.Add(path);
        var parser = new BlameParser();
        var completed = new List<BlameChunk>();
        await foreach (var output in runner
            .StreamAsync(location.CommandPath, arguments, GitRunOptions.ReadOnlyRun, cancellationToken)
            .ConfigureAwait(false))
        {
            parser.Feed(output.Span, completed);
            foreach (var chunk in completed)
            {
                yield return chunk;
            }

            completed.Clear();
        }

        parser.Complete();
        if (warnings is not null)
        {
            foreach (var warning in parser.Warnings)
            {
                warnings.Add(warning);
            }
        }
    }

    private static async Task<BlameContent> ReadWorkingTreeAsync(
        RepositoryLocation location,
        string path,
        CancellationToken cancellationToken)
    {
        if (location.WorkingTreePath is not { } root)
        {
            return new BlameContent(BlameContentKind.Missing, []);
        }

        var file = Path.Combine(root, path);
        if (!File.Exists(file))
        {
            return new BlameContent(BlameContentKind.Missing, []);
        }

        var bytes = await File.ReadAllBytesAsync(file, cancellationToken).ConfigureAwait(false);

        return BlameTextDecoder.Decode(bytes);
    }
}
