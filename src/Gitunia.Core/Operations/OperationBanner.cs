using System.Collections.Immutable;

namespace Gitunia.Core.Operations;

/// <summary>What the banner of an in-progress operation says. Plain data, so the text is testable without a
/// terminal.</summary>
/// <param name="Title">The operation and what it works on, such as <c>MERGING feature/x</c>.</param>
/// <param name="Detail">Step and conflict counts, such as <c>step 2/5  3 conflicts</c>.</param>
/// <param name="Chips">The actions on offer: continue, abort and, where git has it, skip.</param>
/// <param name="Hint">Where the conflict editor opens, shown after the chips only where it fits (#742); null when no
/// conflict is left or its action is unbound.</param>
public sealed record OperationBanner(
    string Title,
    string Detail,
    ImmutableArray<BannerChip> Chips,
    BannerHint? Hint = null)
{
    private const string Gap = "  ";

    /// <summary>The whole banner as one line, for the status bar of a terminal too short for the banner row.</summary>
    public string PlainText
    {
        get
        {
            var parts = new List<string> { Title };
            if (Detail.Length > 0)
            {
                parts.Add(Detail);
            }

            parts.AddRange(Chips.Select(chip => chip.Keys.Length == 0 ? chip.Label : $"{chip.Keys} {chip.Label}"));
            if (Hint is not null)
            {
                parts.Add(Hint.Text);
            }

            return string.Join(Gap, parts);
        }
    }
}
