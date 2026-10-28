namespace Gitunia.Core.Diff;

/// <summary>A stretch of a line's text that differs from the paired line.</summary>
/// <param name="Start">Index of the first changed UTF-16 character.</param>
/// <param name="Length">Number of changed characters.</param>
public readonly record struct ChangedSpan(int Start, int Length)
{
    /// <summary>Index after the last changed character.</summary>
    public int End => Start + Length;
}
