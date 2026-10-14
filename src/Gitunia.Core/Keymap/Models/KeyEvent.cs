namespace Gitunia.Core.Keymap.Models;

/// <summary>
/// A renderer-agnostic key press. <see cref="Name"/> is either the typed text of a printable key
/// (<c>j</c>, <c>G</c>, a space) or a canonical named key from <see cref="KeyNames"/> (<c>Enter</c>, <c>Esc</c>).
/// The renderer adapter must supply the typed text (<c>J</c>, not <c>j</c> plus Shift) for printable keys.
/// </summary>
public readonly record struct KeyEvent(string Name, KeyModifiers Modifiers = KeyModifiers.None)
{
    /// <summary>True when the key types text: no Ctrl or Alt and not a named key.</summary>
    public bool IsPrintable => (Modifiers & (KeyModifiers.Ctrl | KeyModifiers.Alt)) == KeyModifiers.None && !KeyNames.IsNamed(Name);

    /// <summary>Folds Shift into printable text and lowercases Ctrl- letters so equal keys compare equal.</summary>
    public KeyEvent Normalize()
    {
        if (Modifiers.HasFlag(KeyModifiers.Ctrl) && Name.Length == 1)
        {
            return this with { Name = Name.ToLowerInvariant() };
        }

        if (Modifiers.HasFlag(KeyModifiers.Shift) && IsPrintable)
        {
            return this with { Modifiers = Modifiers & ~KeyModifiers.Shift };
        }

        return this;
    }

    /// <summary>Canonical text form that <see cref="KeySequenceParser"/> reads back, e.g. <c>j</c> or <c>&lt;Ctrl-w&gt;</c>.</summary>
    public override string ToString()
    {
        if (Modifiers == KeyModifiers.None && Name == "<")
        {
            return "<lt>";
        }

        if (Modifiers == KeyModifiers.None && !KeyNames.IsNamed(Name) && Name != " ")
        {
            return Name;
        }

        var prefix = string.Concat(
            Modifiers.HasFlag(KeyModifiers.Ctrl) ? "Ctrl-" : string.Empty,
            Modifiers.HasFlag(KeyModifiers.Alt) ? "Alt-" : string.Empty,
            Modifiers.HasFlag(KeyModifiers.Shift) ? "Shift-" : string.Empty);
        return $"<{prefix}{(Name == " " ? "space" : Name)}>";
    }
}
