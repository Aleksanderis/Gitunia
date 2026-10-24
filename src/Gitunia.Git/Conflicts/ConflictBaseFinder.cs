using System.Text;
using Gitunia.Git.Blobs;
using Gitunia.Git.Conflicts.Models;
using Gitunia.Git.Execution;
using Gitunia.Git.Execution.Exceptions;
using Gitunia.Git.Repositories.Models;

namespace Gitunia.Git.Conflicts;

/// <summary>
/// Finds the common-ancestor text of the hunks of a merge-style file (markers without a <c>|||||||</c> section)
/// without touching the working tree: stages 1 to 3 are read with <c>cat-file</c>, written to a private temp
/// directory and merged in memory with <c>merge-file -p --diff3</c>. A hunk of the file gets the base of the first
/// later hunk of that merge whose two sides read the same (line terminators ignored), so a hunk the user already
/// edited, or one git's merge machinery split differently, simply stays without a base.
/// </summary>
internal sealed class ConflictBaseFinder(GitRunner runner)
{
    private const int MaximumConflictCount = 127;
    private const string CarriageReturnLineFeed = "\r\n";
    private const string LineFeed = "\n";

    // merge-file exits with the number of conflicts it left (255 on error).
    private static readonly GitRunOptions MergeFileOptions = new(
        ReadOnly: true,
        AcceptedExitCodes: [.. Enumerable.Range(1, MaximumConflictCount)]);

    /// <summary>
    /// Returns <paramref name="parts"/> with the base filled in where it can be found; never throws for git failures.
    /// </summary>
    public async Task<IReadOnlyList<ConflictPart>> FillAsync(
        RepositoryLocation location,
        string path,
        IReadOnlyList<ConflictPart> parts,
        CancellationToken cancellationToken)
    {
        if (!parts.Any(part => part.Hunk is { Base: null }))
        {
            return parts;
        }

        try
        {
            var merged = await MergeStagesAsync(location, path, cancellationToken).ConfigureAwait(false);
            return merged is null ? parts : Align(parts, ConflictParser.Parse(merged).Parts);
        }
        catch (Exception exception) when (exception is GitException or IOException or UnauthorizedAccessException)
        {
            return parts;
        }
    }

    private static List<ConflictPart> Align(IReadOnlyList<ConflictPart> parts, IReadOnlyList<ConflictPart>? merged)
    {
        var candidates = merged?
            .Where(part => part.Hunk is { Base: not null })
            .Select(part => part.Hunk!)
            .ToList() ?? [];
        var next = 0;
        var result = new List<ConflictPart>(parts.Count);
        foreach (var part in parts)
        {
            if (part.Hunk is not { Base: null } hunk)
            {
                result.Add(part);
                continue;
            }

            var match = candidates.FindIndex(next, candidate => SameSides(hunk, candidate));
            if (match < 0)
            {
                result.Add(part);
                continue;
            }

            next = match + 1;
            result.Add(ConflictPart.OfHunk(hunk with { Base = candidates[match].Base }));
        }

        return result;
    }

    private static bool SameSides(ConflictHunk left, ConflictHunk right) =>
        Plain(left.Ours) == Plain(right.Ours) && Plain(left.Theirs) == Plain(right.Theirs);

    private static string Plain(string text) =>
        text.Replace(CarriageReturnLineFeed, LineFeed, StringComparison.Ordinal);

    private async Task<string?> MergeStagesAsync(
        RepositoryLocation location,
        string path,
        CancellationToken cancellationToken)
    {
        var directory = Path.Combine(Path.GetTempPath(), $"gitunia-merge-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            string[] names = ["base", "ours", "theirs"];
            for (var stage = 1; stage <= names.Length; stage++)
            {
                var blob = await runner
                    .RunAsync(
                        location.CommandPath,
                        ["cat-file", "blob", $":{stage}:{path}"],
                        GitRunOptions.ReadOnlyRun,
                        cancellationToken)
                    .ConfigureAwait(false);
                if (blob.StandardOutput.Length > BlobReader.MaximumBytes)
                {
                    return null;
                }

                await File.WriteAllBytesAsync(
                    Path.Combine(directory, names[stage - 1]),
                    blob.StandardOutput,
                    cancellationToken).ConfigureAwait(false);
            }

            var merge = await runner
                .RunAsync(
                    directory,
                    ["merge-file", "-p", "--diff3", "ours", "base", "theirs"],
                    MergeFileOptions,
                    cancellationToken)
                .ConfigureAwait(false);
            return new UTF8Encoding(false, true).GetString(merge.StandardOutput);
        }
        catch (DecoderFallbackException)
        {
            return null;
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
