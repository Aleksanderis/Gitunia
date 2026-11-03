using System.Globalization;
using Gitunia.Core.Editing;

namespace Gitunia.Core.Diff;

/// <summary>A search inside one diff document.</summary>
/// <param name="Phase">None, typing or accepted.</param>
/// <param name="Text">The query typed so far.</param>
/// <param name="Matches">Ascending rows whose text contains the query; empty for an empty query.</param>
/// <param name="Notice">A short remark such as "No match", empty when none.</param>
/// <param name="Caret">The caret column in <see cref="Text"/> while typing (see <see cref="PromptCaret"/>).</param>
public sealed record DiffSearchState(DiffSearchPhase Phase, string Text, IReadOnlyList<int> Matches, string Notice,
    int Caret = PromptCaret.End)
{
    public static DiffSearchState Idle { get; } = new(DiffSearchPhase.None, string.Empty, [], string.Empty);

    /// <summary>The query is shown and highlighted (typing or accepted).</summary>
    public bool IsOpen => Phase != DiffSearchPhase.None;

    /// <summary>The text to highlight in rows, null when no search is open or the query is empty.</summary>
    public string? Highlight => IsOpen && Text.Length > 0 ? Text : null;

    /// <summary>The status bar text: <c>/text_  3/17</c> while typing, <c>/text  3/17</c> afterwards; empty when idle.</summary>
    /// <param name="row">The selected row, to tell which match the cursor is on.</param>
    public string Describe(int row)
    {
        if (!IsOpen)
        {
            return string.Empty;
        }

        var shown = Phase == DiffSearchPhase.Editing ? PromptCaret.Mark(Text, Caret) : Text;
        var position = Matches.Count == 0 ? "0" : PositionOf(row);
        var text = $"/{shown}  {position}/{Matches.Count}";
        return Notice.Length > 0 ? $"{text}  {Notice}" : text;
    }

    private string PositionOf(int row)
    {
        for (var index = 0; index < Matches.Count; index++)
        {
            if (Matches[index] == row)
            {
                return (index + 1).ToString(CultureInfo.InvariantCulture);
            }
        }

        return "-";
    }
}
