namespace Gitunia.Core.Config;

/// <summary>Skips whitespace and comments, the parts of the config text that carry no data.</summary>
public static class ConfigTrivia
{
    private const string LineCommentStart = "//";
    private const string BlockCommentStart = "/*";
    private const string BlockCommentEnd = "*/";

    /// <summary>The index of the first character at or after <paramref name="index"/> that is not trivia.</summary>
    /// <param name="text">The config text.</param>
    /// <param name="index">Where to start.</param>
    public static int Skip(string text, int index)
    {
        var position = index;
        while (position < text.Length)
        {
            if (char.IsWhiteSpace(text[position]))
            {
                position++;
            }
            else if (string.CompareOrdinal(text, position, LineCommentStart, 0, LineCommentStart.Length) == 0)
            {
                var end = text.IndexOf('\n', position);
                position = end < 0 ? text.Length : end + 1;
            }
            else if (string.CompareOrdinal(text, position, BlockCommentStart, 0, BlockCommentStart.Length) == 0)
            {
                var end = text.IndexOf(BlockCommentEnd, position + BlockCommentStart.Length, StringComparison.Ordinal);
                position = end < 0 ? text.Length : end + BlockCommentEnd.Length;
            }
            else
            {
                break;
            }
        }

        return position;
    }
}
