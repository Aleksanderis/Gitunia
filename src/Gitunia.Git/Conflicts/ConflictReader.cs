using System.Text;
using Gitunia.Git.Blobs;
using Gitunia.Git.Conflicts.Models;
using Gitunia.Git.Execution;
using Gitunia.Git.Repositories.Models;

namespace Gitunia.Git.Conflicts;

/// <summary>
/// Reads a conflicted path. <c>ls-files -u -z</c> names the stages that exist (1 base, 2 ours, 3 theirs): a missing
/// stage is add/add or delete/modify, and a symlink or submodule entry is not text; none of these is editable here.
/// The hunks come from the file git left in the working tree, because its markers hold exactly the conflicting
/// text of each side (and the base for diff3) and show what the user already changed by hand.
/// </summary>
public sealed class ConflictReader(GitRunner runner) : IConflictReader
{
    /// <summary>The largest file that is read (the same cap as the Files tab).</summary>
    public const int MaximumBytes = BlobReader.MaximumBytes;

    private const string ListFiles = "ls-files";
    private const string Unmerged = "-u";
    private const int BaseStage = 1;
    private const int OursStage = 2;
    private const int TheirsStage = 3;
    private const string SymlinkMode = "120000";
    private const string SubmoduleMode = "160000";
    private const char FieldSeparator = ' ';
    private const char PathSeparator = '\t';

    public async Task<ConflictReadResult> ReadAsync(
        RepositoryLocation location,
        string path,
        CancellationToken cancellationToken = default)
    {
        if (location.WorkingTreePath is not { } root)
        {
            return ConflictReadResult.Unsupported("A bare repository has no files to resolve");
        }

        if (GitPathText.HasEscapedBytes(path))
        {
            return ConflictReadResult.Unsupported("The file name is not valid UTF-8: use the mergetool");
        }

        var result = await runner
            .RunAsync(
                location.CommandPath,
                [ListFiles, Unmerged, GitOptions.NulTerminated, GitOptions.EndOfOptions, Literal(path)],
                GitRunOptions.ReadOnlyRun,
                cancellationToken)
            .ConfigureAwait(false);
        var entries = Stages(result.StandardOutput);
        if (UnsupportedStages(entries) is { } reason)
        {
            return ConflictReadResult.Unsupported(reason);
        }

        return await ReadFileAsync(Path.Combine(root, path), cancellationToken).ConfigureAwait(false);
    }

    private static async Task<ConflictReadResult> ReadFileAsync(string fullPath, CancellationToken cancellationToken)
    {
        FileInfo file = new(fullPath);
        if (!file.Exists)
        {
            return ConflictReadResult.Unsupported("The file is not in the working tree: use the mergetool");
        }

        if (file.Length > MaximumBytes)
        {
            return ConflictReadResult.Unsupported("The file is too large to resolve here: use the mergetool");
        }

        var bytes = await File.ReadAllBytesAsync(fullPath, cancellationToken).ConfigureAwait(false);
        if (bytes.AsSpan(0, Math.Min(bytes.Length, BlobReader.BinaryProbeBytes)).IndexOf((byte)0) >= 0)
        {
            return ConflictReadResult.Unsupported("A binary file cannot be resolved here: use the mergetool");
        }

        string text;
        try
        {
            text = new UTF8Encoding(false, true).GetString(bytes);
        }
        catch (DecoderFallbackException)
        {
            return ConflictReadResult.Unsupported("The file is not valid UTF-8: use the mergetool");
        }

        var parsed = ConflictParser.Parse(text);
        if (parsed.Parts is not { } parts)
        {
            return ConflictReadResult.Unsupported($"{parsed.Problem}: use the mergetool");
        }

        return parts.Any(part => part.Hunk is not null)
            ? ConflictReadResult.OfText(parts)
            : ConflictReadResult.Unsupported("The file has no conflict markers left: use the mergetool");
    }

    private static string? UnsupportedStages(List<StageEntry> entries)
    {
        if (entries.Count == 0)
        {
            return "The file is not conflicted any more";
        }

        if (entries.Any(entry => entry.Mode is SymlinkMode or SubmoduleMode))
        {
            return "A symlink or submodule cannot be resolved here: use the mergetool";
        }

        var hasOurs = entries.Any(entry => entry.Stage == OursStage);
        var hasTheirs = entries.Any(entry => entry.Stage == TheirsStage);
        if (!hasOurs || !hasTheirs)
        {
            return "One side deleted the file (delete/modify): use the mergetool";
        }

        return entries.Any(entry => entry.Stage == BaseStage)
            ? null
            : "Both sides added the file (add/add): use the mergetool";
    }

    // Records are `<mode> <object> <stage>\t<path>`, NUL separated.
    private static List<StageEntry> Stages(byte[] output)
    {
        var entries = new List<StageEntry>();
        foreach (var record in Encoding.UTF8.GetString(output).Split('\0', StringSplitOptions.RemoveEmptyEntries))
        {
            var tab = record.IndexOf(PathSeparator, StringComparison.Ordinal);
            var fields = (tab < 0 ? record : record[..tab]).Split(FieldSeparator);
            if (fields.Length == 3 && int.TryParse(fields[2], out var stage))
            {
                entries.Add(new StageEntry(fields[0], stage));
            }
        }

        return entries;
    }

    private static string Literal(string path) => $"{GitOptions.LiteralPathspecPrefix}{path}";

    private readonly record struct StageEntry(string Mode, int Stage);
}
