namespace Gitunia.Core.Keymap.Models;

/// <summary>Input mode that decides which bindings are active and how printable keys are treated.</summary>
public enum KeyMode
{
    /// <summary>Navigate and act; counts and chords are available.</summary>
    Normal,

    /// <summary>Select a range of lines or commits; behaves like Normal for counts and chords.</summary>
    Visual,

    /// <summary>Text input: printable keys are text, only Ctrl-/Alt- combinations and named keys act.</summary>
    Insert,

    /// <summary>Command palette input; printable keys are text like in Insert.</summary>
    Command,

    /// <summary>Yes/no dialog; only its bindings act.</summary>
    Confirm,
}
