namespace Gitunia.Git.Diff.Models;

/// <summary>
/// One <c>@@</c> block. A line is addressed by its index in the parsed hunk and file lists; the
/// <see cref="HeaderText"/> is kept verbatim so the hunk can be written back exactly.
/// </summary>
/// <param name="HeaderText">The full <c>@@ -a,b +c,d @@ section</c> line.</param>
/// <param name="Section">The text after the closing <c>@@</c> (usually the enclosing function), or empty.</param>
public sealed record DiffHunk(
    string HeaderText,
    int OldStart,
    int OldCount,
    int NewStart,
    int NewCount,
    string Section,
    IReadOnlyList<DiffLine> Lines);
