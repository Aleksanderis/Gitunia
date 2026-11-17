using System.Collections.Immutable;
using Gitunia.Core.Editing;
using Gitunia.Core.Lists;

namespace Gitunia.Core.Search.Models;

/// <summary>The ref jump prompt (<c>&lt;space&gt;f</c>): the typed text, the ranked candidates and which one <c>Enter</c> picks.</summary>
/// <param name="Field">The typed text and its caret.</param>
/// <param name="Candidates">Best match first.</param>
/// <param name="SelectedIndex">The highlighted candidate.</param>
public sealed record RefJumpState(TextField Field, ImmutableArray<RefJumpCandidate> Candidates, int SelectedIndex)
{
    /// <summary>The typed text.</summary>
    public string Text => Field.Text;

    /// <summary>The candidate <c>Enter</c> picks, null when nothing matches.</summary>
    public RefJumpCandidate? Selected => SelectedIndex >= 0 && SelectedIndex < Candidates.Length ? Candidates[SelectedIndex] : null;

    /// <summary>Moves the highlight by <paramref name="delta"/> candidates, stopping at the ends.</summary>
    public RefJumpState Move(int delta) =>
        this with { SelectedIndex = ListViewport.ClampIndex(SelectedIndex + delta, Candidates.Length) };
}
