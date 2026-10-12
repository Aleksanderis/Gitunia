namespace Gitunia.Core.Diff;

/// <summary>What one row of the diff pane is.</summary>
public enum DiffRowKind
{
    /// <summary>A parser warning (for example invalid UTF-8 in the patch), shown above the first file.</summary>
    Warning,

    /// <summary>The line that starts a file: status mark and path.</summary>
    FileHeader,

    /// <summary>A remark about a file that has no hunks or a special state (binary, rename without edits, mode change).</summary>
    Note,

    /// <summary>The <c>@@ -a,b +c,d @@</c> line.</summary>
    HunkHeader,
    Context,
    Added,
    Removed,

    /// <summary>The <c>\ No newline at end of file</c> marker.</summary>
    NoNewline,
}
