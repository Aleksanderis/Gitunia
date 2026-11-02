namespace Gitunia.Git.Blame.Models;

/// <summary>The switches that change what <c>git blame</c> attributes (the ignore-revs file is git's own config).</summary>
/// <param name="IgnoreWhitespace"><c>-w</c>: whitespace changes do not take a line away from its author.</param>
/// <param name="DetectMoves"><c>-M</c>: lines moved inside the file keep their first author.</param>
/// <param name="DetectCopies"><c>-C</c>: lines copied from other files changed in the same commit keep their first author.</param>
public sealed record BlameOptions(bool IgnoreWhitespace = false, bool DetectMoves = false, bool DetectCopies = false)
{
    /// <summary>No switch set.</summary>
    public static BlameOptions None { get; } = new();

    /// <summary>The git arguments of the set switches.</summary>
    public IEnumerable<string> Arguments()
    {
        if (IgnoreWhitespace)
        {
            yield return "-w";
        }

        if (DetectMoves)
        {
            yield return "-M";
        }

        if (DetectCopies)
        {
            yield return "-C";
        }
    }
}
