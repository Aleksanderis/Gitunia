using System.Text;
using Gitunia.Git.Repositories.Models;

namespace Gitunia.Git.Ignore;

/// <summary>Appends patterns to the root <c>.gitignore</c> of a working tree (see
/// <see cref="IIgnoreWriter"/>).</summary>
public sealed class IgnoreWriter : IIgnoreWriter
{
    /// <summary>The name of the file at the root of the working tree.</summary>
    public const string FileName = ".gitignore";

    private const char LineFeed = '\n';
    private const string CarriageReturnLineFeed = "\r\n";
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(10);
    private static readonly UTF8Encoding _utf8 = new(false);

    /// <inheritdoc />
    public async Task<IgnoreAddOutcome> AddAsync(
        RepositoryLocation location,
        string pattern,
        CancellationToken cancellationToken)
    {
        if (location.WorkingTreePath is not { } root)
        {
            throw new InvalidOperationException("A bare repository has no working tree to ignore files in");
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_timeout);
        var token = timeout.Token;
        var path = Path.Combine(root, FileName);
        var existing = string.Empty;
        if (File.Exists(path))
        {
            existing = await File.ReadAllTextAsync(path, token).ConfigureAwait(false);
        }

        if (Lists(existing, pattern))
        {
            return IgnoreAddOutcome.AlreadyListed;
        }

        var newline = existing.Contains(CarriageReturnLineFeed, StringComparison.Ordinal)
            ? CarriageReturnLineFeed
            : LineFeed.ToString();
        var separator = existing.Length == 0 || existing.EndsWith(LineFeed) ? string.Empty : newline;
        await File.AppendAllTextAsync(path, $"{separator}{pattern}{newline}", _utf8, token).ConfigureAwait(false);

        return IgnoreAddOutcome.Added;
    }

    private static bool Lists(string content, string pattern)
    {
        foreach (var line in content.Split(LineFeed))
        {
            if (line.TrimEnd('\r') == pattern)
            {
                return true;
            }
        }

        return false;
    }
}
