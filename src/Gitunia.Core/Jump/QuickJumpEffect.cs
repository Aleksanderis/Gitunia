namespace Gitunia.Core.Jump;

/// <summary>Move the cursor of the list the quick jump labelled to a row and scroll it into view.</summary>
/// <param name="Row">The row of the list's own rows.</param>
public sealed record JumpToRowEffect(int Row) : AppEffect;
