using System.Collections.Immutable;

namespace Gitunia.Core.Search;

/// <summary>The <c>gr</c> prompt: the typed text, the ranked candidates and which one <c>Enter</c> picks.</summary>
/// <param name="Text">The typed text.</param>
/// <param name="Candidates">Best match first.</param>
/// <param name="SelectedIndex">The highlighted candidate.</param>
public sealed record RefJumpState(string Text, ImmutableArray<RefJumpCandidate> Candidates, int SelectedIndex)
{
    /// <summary>The candidate <c>Enter</c> picks, null when nothing matches.</summary>
    public RefJumpCandidate? Selected => SelectedIndex >= 0 && SelectedIndex < Candidates.Length ? Candidates[SelectedIndex] : null;

    /// <summary>Moves the highlight by <paramref name="delta"/> candidates, wrapping around.</summary>
    public RefJumpState Move(int delta)
    {
        if (Candidates.IsEmpty)
        {
            return this;
        }

        var count = Candidates.Length;
        return this with { SelectedIndex = (((SelectedIndex + delta) % count) + count) % count };
    }
}
