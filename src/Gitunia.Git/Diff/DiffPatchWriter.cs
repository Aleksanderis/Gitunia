using System.Globalization;
using System.Text;
using Gitunia.Git.Diff.Models;

namespace Gitunia.Git.Diff;

/// <summary>Turns parsed diffs back into patch text for <c>git apply</c>: whole files, whole hunks or chosen lines.</summary>
public static class DiffPatchWriter
{
    private const string NoNewlineText = "\\ No newline at end of file";

    /// <summary>The file exactly as git printed it (header lines, then every hunk).</summary>
    public static string WriteFile(DiffFile file)
    {
        ThrowIfInexact(file);
        var builder = new StringBuilder();
        AppendHeader(builder, file);
        foreach (var hunk in file.Hunks)
        {
            AppendHunk(builder, hunk);
        }

        return builder.ToString();
    }

    /// <summary>The hunk at <paramref name="hunkIndex"/> exactly as git printed it (without the file header).</summary>
    public static string WriteHunk(DiffFile file, int hunkIndex)
    {
        ThrowIfInexact(file);
        var builder = new StringBuilder();
        AppendHunk(builder, file.Hunks[hunkIndex]);
        return builder.ToString();
    }

    /// <summary>The file header followed by the chosen hunks, unchanged.</summary>
    public static string WriteHunks(DiffFile file, IReadOnlyCollection<int> hunkIndexes)
    {
        ThrowIfInexact(file);
        var builder = new StringBuilder();
        AppendHeader(builder, file);
        for (var index = 0; index < file.Hunks.Count; index++)
        {
            if (hunkIndexes.Contains(index))
            {
                AppendHunk(builder, file.Hunks[index]);
            }
        }

        return builder.ToString();
    }

    /// <summary>
    /// A patch that applies only the selected added and removed lines. Hunk and line indexes address
    /// <see cref="DiffFile.Hunks"/> and <see cref="DiffHunk.Lines"/>. In a <see cref="PatchDirection.Forward"/> patch an
    /// unselected removed line becomes context and an unselected added line is dropped; in a
    /// <see cref="PatchDirection.Reverse"/> patch it is the other way round. Counts and offsets in the hunk headers are
    /// recomputed. Returns an empty string if no change is selected. Limits: not meaningful for binary files or for
    /// added/deleted files (stage those whole), and a hunk produced with zero context lines is not re-anchored.
    /// </summary>
    public static string WriteSelection(
        DiffFile file,
        IReadOnlyDictionary<int, IReadOnlyCollection<int>> selectedLines,
        PatchDirection direction)
    {
        ThrowIfInexact(file);
        var body = new StringBuilder();
        var offset = 0;
        for (var hunkIndex = 0; hunkIndex < file.Hunks.Count; hunkIndex++)
        {
            if (!selectedLines.TryGetValue(hunkIndex, out var selected))
            {
                continue;
            }

            var hunk = file.Hunks[hunkIndex];
            var lines = new StringBuilder();
            var oldCount = 0;
            var newCount = 0;
            var hasChange = false;
            var effectiveSelection = CoupleNoNewlineLines(hunk, selected);
            for (var lineIndex = 0; lineIndex < hunk.Lines.Count; lineIndex++)
            {
                var line = hunk.Lines[lineIndex];
                var kind = EffectiveKind(line.Kind, effectiveSelection.Contains(lineIndex), direction);
                if (kind is null)
                {
                    continue;
                }

                hasChange |= kind != DiffLineKind.Context;
                oldCount += kind == DiffLineKind.Added ? 0 : 1;
                newCount += kind == DiffLineKind.Removed ? 0 : 1;
                AppendLine(lines, kind.Value, line);
            }

            if (!hasChange)
            {
                continue;
            }

            var oldStart = direction == PatchDirection.Forward ? hunk.OldStart : hunk.NewStart - offset;
            var newStart = direction == PatchDirection.Forward ? hunk.OldStart + offset : hunk.NewStart;
            body.Append(CultureFree($"@@ -{oldStart},{oldCount} +{newStart},{newCount} @@"));
            body.Append(hunk.Section.Length > 0 ? $" {hunk.Section}" : string.Empty);
            body.Append('\n').Append(lines);
            offset += newCount - oldCount;
        }

        if (body.Length == 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        AppendHeader(builder, file);
        return builder.Append(body).ToString();
    }

    /// <summary>
    /// A line followed by the no-newline marker changes whether the file ends with a newline, which only makes sense
    /// together with the opposite change next to it. If such a line is unselected while a line of the other kind is
    /// selected, it is selected too so the patch stays applicable.
    /// </summary>
    private static HashSet<int> CoupleNoNewlineLines(DiffHunk hunk, IReadOnlyCollection<int> selected)
    {
        var result = new HashSet<int>(selected);
        var selectsAdded = selected.Any(index => index >= 0 && index < hunk.Lines.Count && hunk.Lines[index].Kind == DiffLineKind.Added);
        var selectsRemoved = selected.Any(index => index >= 0 && index < hunk.Lines.Count && hunk.Lines[index].Kind == DiffLineKind.Removed);
        for (var index = 0; index < hunk.Lines.Count; index++)
        {
            var line = hunk.Lines[index];
            if (!line.NoNewlineAtEnd || result.Contains(index))
            {
                continue;
            }

            if ((line.Kind == DiffLineKind.Removed && selectsAdded) || (line.Kind == DiffLineKind.Added && selectsRemoved))
            {
                result.Add(index);
            }
        }

        return result;
    }

    private static void ThrowIfInexact(DiffFile file)
    {
        if (file.HasInvalidText)
        {
            throw new InexactPatchException(file.Path);
        }
    }

    private static string CultureFree(FormattableString text) => text.ToString(CultureInfo.InvariantCulture);

    /// <summary>What a line becomes in the partial patch, or null if it is left out.</summary>
    private static DiffLineKind? EffectiveKind(DiffLineKind kind, bool isSelected, PatchDirection direction)
    {
        if (kind == DiffLineKind.Context || isSelected)
        {
            return kind;
        }

        var keptAsContext = direction == PatchDirection.Forward ? DiffLineKind.Removed : DiffLineKind.Added;
        return kind == keptAsContext ? DiffLineKind.Context : null;
    }

    private static void AppendHeader(StringBuilder builder, DiffFile file)
    {
        foreach (var line in file.HeaderLines)
        {
            builder.Append(line).Append('\n');
        }
    }

    private static void AppendHunk(StringBuilder builder, DiffHunk hunk)
    {
        builder.Append(hunk.HeaderText).Append('\n');
        foreach (var line in hunk.Lines)
        {
            AppendLine(builder, line.Kind, line);
        }
    }

    private static void AppendLine(StringBuilder builder, DiffLineKind kind, DiffLine line)
    {
        var prefix = kind switch
        {
            DiffLineKind.Added => '+',
            DiffLineKind.Removed => '-',
            _ => ' ',
        };
        builder.Append(prefix).Append(line.Text).Append('\n');
        if (line.NoNewlineAtEnd)
        {
            builder.Append(NoNewlineText).Append('\n');
        }
    }
}
