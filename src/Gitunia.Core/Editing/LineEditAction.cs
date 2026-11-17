namespace Gitunia.Core.Editing;

/// <summary>What a key does inside one line of text; the one-line subset shared by every text editor (see <see cref="LineEditKeys"/>).</summary>
public enum LineEditAction
{
    /// <summary>Delete the grapheme before the caret.</summary>
    Backspace,

    /// <summary>Delete the grapheme after the caret.</summary>
    Delete,

    /// <summary>Move one grapheme left.</summary>
    Left,

    /// <summary>Move one grapheme right.</summary>
    Right,

    /// <summary>Move to the start of the line.</summary>
    LineStart,

    /// <summary>Move to the end of the line.</summary>
    LineEnd,

    /// <summary>Move to the start of the word.</summary>
    WordLeft,

    /// <summary>Move to the end of the word.</summary>
    WordRight,

    /// <summary>Delete from the start of the line to the caret.</summary>
    DeleteToStart,

    /// <summary>Delete from the caret to the end of the line.</summary>
    DeleteToEnd,

    /// <summary>Delete the word before the caret.</summary>
    DeleteWordBack,

    /// <summary>Delete the word after the caret.</summary>
    DeleteWordForward,
}
