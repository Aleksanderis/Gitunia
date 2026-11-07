namespace Gitunia.Core.Operations;

/// <summary>Where the conflict editor (or the mergetool) opens, shown after the banner chips. Keys and label travel
/// apart so a key that contains a space, such as a remapped <c>Ctrl-w c</c>, is never split by re-parsing text (#1244).</summary>
/// <param name="Keys">The keys in the notation of <c>docs/internal/ux-keymap.md</c>.</param>
/// <param name="Label">The long label, such as <c>open conflict editor</c>.</param>
/// <param name="ShortLabel">The label in fewer columns, tried when the long form does not fit.</param>
public sealed record BannerHint(string Keys, string Label, string ShortLabel)
{
    /// <summary>The hint as one text with the long label.</summary>
    public string Text => $"{Keys} {Label}";

    /// <summary>The hint as one text with the short label.</summary>
    public string ShortText => $"{Keys} {ShortLabel}";
}
