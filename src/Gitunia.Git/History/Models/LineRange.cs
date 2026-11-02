using System.Globalization;

namespace Gitunia.Git.History.Models;

/// <summary>A range of lines of a file, one-based and inclusive, as <c>git log -L</c> takes it.</summary>
/// <param name="Start">The first line.</param>
/// <param name="End">The last line, not before <paramref name="Start"/>.</param>
public readonly record struct LineRange(int Start, int End)
{
    /// <summary>The range as people write it: <c>12</c> for one line, <c>12-20</c> for several.</summary>
    public string Label => Start == End
        ? Start.ToString(CultureInfo.InvariantCulture)
        : string.Create(CultureInfo.InvariantCulture, $"{Start}-{End}");
}
