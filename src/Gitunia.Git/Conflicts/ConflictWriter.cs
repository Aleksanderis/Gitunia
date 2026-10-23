using System.Text;
using Gitunia.Git.Repositories.Models;

namespace Gitunia.Git.Conflicts;

/// <summary>
/// Overwrites the file in place (so its permissions and a symlinked location survive). A failed write throws the
/// file system's exception and may leave the file half written; <c>git checkout --merge -- &lt;file&gt;</c> re-creates
/// the conflict.
/// </summary>
public sealed class ConflictWriter : IConflictWriter
{
    private const string ParentSegment = "..";

    public async Task WriteAsync(
        RepositoryLocation location,
        string path,
        string text,
        CancellationToken cancellationToken = default)
    {
        if (location.WorkingTreePath is not { } root)
        {
            throw new ArgumentException("A bare repository has no working tree to write to", nameof(location));
        }

        var fullPath = Path.GetFullPath(Path.Combine(root, path));
        var relative = Path.GetRelativePath(root, fullPath);
        var leavesRoot = relative == ParentSegment
            || relative.StartsWith($"{ParentSegment}{Path.DirectorySeparatorChar}", StringComparison.Ordinal);
        if (Path.IsPathRooted(path) || leavesRoot)
        {
            throw new ArgumentException("The path is outside the working tree", nameof(path));
        }

        var bytes = new UTF8Encoding(false).GetBytes(text);
        await File.WriteAllBytesAsync(fullPath, bytes, cancellationToken).ConfigureAwait(false);
    }
}
