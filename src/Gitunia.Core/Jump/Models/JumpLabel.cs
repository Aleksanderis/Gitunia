namespace Gitunia.Core.Jump.Models;

/// <summary>One quick-jump target: a row whose text contains the typed text, and the key that jumps to it.</summary>
/// <param name="Row">The row (commit index) the label jumps to.</param>
/// <param name="MatchStart">Where the typed text starts in the row's subject.</param>
/// <param name="MatchLength">Length of the match.</param>
/// <param name="Letter">The label key, or <see cref="NoLetter"/> when the matches outnumber the alphabet (type more to narrow them).</param>
public readonly record struct JumpLabel(int Row, int MatchStart, int MatchLength, char Letter)
{
    /// <summary>The letter of a match that got no label.</summary>
    public const char NoLetter = '\0';

    public bool HasLetter => Letter != NoLetter;
}
