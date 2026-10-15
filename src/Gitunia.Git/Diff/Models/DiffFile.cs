namespace Gitunia.Git.Diff.Models;

/// <summary>One file of a diff.</summary>
/// <param name="OldPath">Path on the old side; null for added files.</param>
/// <param name="NewPath">Path on the new side; null for deleted files.</param>
/// <param name="OldMode">File mode on the old side (for example <c>100644</c>), or null if the diff does not say.</param>
/// <param name="NewMode">File mode on the new side, or null.</param>
/// <param name="Similarity">Similarity percentage of a rename or copy, or null.</param>
/// <param name="IsBinary">Binary file: no hunks, only <see cref="HeaderLines"/> (which include the binary patch if it was requested).</param>
/// <param name="HeaderLines">Every line from <c>diff --git</c> up to the first hunk, verbatim and without terminators.</param>
public sealed record DiffFile(
    string? OldPath,
    string? NewPath,
    DiffFileStatus Status,
    string? OldMode,
    string? NewMode,
    int? Similarity,
    bool IsBinary,
    IReadOnlyList<string> HeaderLines,
    IReadOnlyList<DiffHunk> Hunks)
{
    /// <summary>The path to show: the new path, or the old one for deleted files.</summary>
    public string Path => NewPath ?? OldPath ?? string.Empty;

    /// <summary>Hunk lines beyond the parser's per-file limit were dropped; the hunks hold only the first lines.</summary>
    public bool IsTruncated { get; init; }

    /// <summary>
    /// A line of this file (path or content) was not valid UTF-8 and was replaced while decoding, so patch text built
    /// from it is not an exact copy; <see cref="DiffPatchWriter"/> refuses to build one. Stage such files whole by path.
    /// </summary>
    public bool HasInvalidText { get; init; }

    public bool IsModeChange => OldMode is not null && NewMode is not null && OldMode != NewMode;
}
