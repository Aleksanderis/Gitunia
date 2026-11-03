using Gitunia.Core.Editing;

namespace Gitunia.Core.Reflog;

/// <summary>
/// The ref picker of the reflog list: a filtered list of <c>HEAD</c> and the local branches, so the reflog of any
/// branch can be opened without leaving the list. Pure.
/// </summary>
/// <param name="Names">Every choice, <c>HEAD</c> first, then the local branches.</param>
/// <param name="Text">The typed filter (case-insensitive substring).</param>
/// <param name="Caret">The caret column in <see cref="Text"/> (see <see cref="PromptCaret"/>).</param>
/// <param name="Index">Index into <see cref="Candidates"/>.</param>
public sealed record ReflogRefPicker(IReadOnlyList<string> Names, string Text = "", int Index = 0, int Caret = PromptCaret.End)
{
    /// <summary>The choices that match the typed text, in their original order.</summary>
    public IReadOnlyList<string> Candidates
    {
        get
        {
            if (Text.Length == 0)
            {
                return Names;
            }

            return [.. Names.Where(name => name.Contains(Text, StringComparison.OrdinalIgnoreCase))];
        }
    }

    /// <summary>The highlighted choice, null when nothing matches.</summary>
    public string? Selected =>
        Candidates is { Count: > 0 } candidates ? candidates[Math.Clamp(Index, 0, candidates.Count - 1)] : null;

    /// <summary>The picker with new filter text; the highlight returns to the first match.</summary>
    public ReflogRefPicker WithText(string text) => this with { Text = text, Index = 0 };

    /// <summary>The picker after an edit of the filter; a move of the caret alone keeps the highlight.</summary>
    public ReflogRefPicker WithEdit(PromptEdit edit) =>
        edit.Text == Text ? this with { Caret = edit.Caret } : this with { Text = edit.Text, Caret = edit.Caret, Index = 0 };

    /// <summary>Moves the highlight by <paramref name="step"/>, wrapping around the matches.</summary>
    public ReflogRefPicker Move(int step)
    {
        var count = Candidates.Count;
        return count == 0 ? this : this with { Index = (((Index + step) % count) + count) % count };
    }

    /// <summary>Highlights the choice at <paramref name="index"/> of <see cref="Candidates"/>, clamped.</summary>
    public ReflogRefPicker MoveTo(int index) =>
        this with { Index = Candidates.Count == 0 ? 0 : Math.Clamp(index, 0, Candidates.Count - 1) };
}
