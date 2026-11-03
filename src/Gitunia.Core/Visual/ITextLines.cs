namespace Gitunia.Core.Visual;

/// <summary>The text lines of a read-only pane, which a character-wise selection reads.</summary>
public interface ITextLines
{
    /// <summary>How many lines there are.</summary>
    int Count { get; }

    /// <summary>The plain text of a line, as it is copied (no gutter, no markers).</summary>
    /// <param name="line">The line.</param>
    string TextOf(int line);

    /// <summary>The line is left out of a copy (a note or a warning the diff shows but the file does not have).</summary>
    /// <param name="line">The line.</param>
    bool IsSkipped(int line);
}
