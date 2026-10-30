namespace Gitunia.Core.Conflicts;

/// <summary>A Normal mode command of the result pane (the commit box's vim subset).</summary>
public enum ConflictResultCommand
{
    /// <summary><c>h</c>.</summary>
    Left,

    /// <summary><c>l</c>.</summary>
    Right,

    /// <summary><c>w</c>.</summary>
    WordNext,

    /// <summary><c>b</c>.</summary>
    WordPrevious,

    /// <summary><c>0</c>.</summary>
    LineStart,

    /// <summary><c>$</c>.</summary>
    LineEnd,

    /// <summary><c>x</c>.</summary>
    DeleteChar,

    /// <summary><c>dd</c>.</summary>
    DeleteLine,

    /// <summary><c>o</c>: opens a line below and starts Insert mode.</summary>
    OpenLine,

    /// <summary><c>a</c>: Insert mode after the cursor.</summary>
    Append,

    /// <summary><c>A</c>: Insert mode at the line end.</summary>
    AppendAtEnd,

    /// <summary><c>I</c>: Insert mode at the line start.</summary>
    InsertAtStart,
}
