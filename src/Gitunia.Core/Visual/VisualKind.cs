namespace Gitunia.Core.Visual;

/// <summary>What a Visual-mode selection covers.</summary>
public enum VisualKind
{
    /// <summary>Whole rows (<c>v</c>, a drag in a list).</summary>
    Lines,

    /// <summary>Characters inside rows (the character-wise key, a drag in a text pane).</summary>
    Chars,
}
