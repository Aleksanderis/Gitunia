using System.Text;
using Gitunia.Git.Blobs.Models;
using Gitunia.Git.Execution;
using Gitunia.Git.Repositories.Models;

namespace Gitunia.Git.Blobs;

/// <summary>Reads a blob with <c>git cat-file blob</c>, capped at <see cref="MaximumBytes"/>.</summary>
public sealed class BlobReader(GitRunner runner) : IBlobReader
{
    /// <summary>The largest blob that is read and shown (1 MiB).</summary>
    public const int MaximumBytes = 1024 * 1024;

    /// <summary>How many leading bytes are searched for a NUL byte to tell binary from text (git looks at 8000).</summary>
    public const int BinaryProbeBytes = 8000;

    private const string CatFile = "cat-file";
    private const string BlobType = "blob";

    public async Task<BlobContent> ReadAsync(
        RepositoryLocation location,
        ObjectId blob,
        long? knownSize = null,
        CancellationToken cancellationToken = default)
    {
        if (knownSize > MaximumBytes)
        {
            return new BlobContent(BlobKind.TooLarge, string.Empty, knownSize.Value);
        }

        var result = await runner
            .RunAsync(
                location.CommandPath,
                [CatFile, BlobType, blob.ToString()],
                GitRunOptions.ReadOnlyRun,
                cancellationToken)
            .ConfigureAwait(false);
        var bytes = result.StandardOutput;
        if (bytes.Length > MaximumBytes)
        {
            return new BlobContent(BlobKind.TooLarge, string.Empty, bytes.Length);
        }

        var probe = bytes.AsSpan(0, Math.Min(bytes.Length, BinaryProbeBytes));
        return probe.IndexOf((byte)0) >= 0
            ? new BlobContent(BlobKind.Binary, string.Empty, bytes.Length)
            : new BlobContent(BlobKind.Text, Encoding.UTF8.GetString(bytes), bytes.Length);
    }
}
