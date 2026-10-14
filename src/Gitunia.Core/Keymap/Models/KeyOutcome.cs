namespace Gitunia.Core.Keymap.Models;

public enum KeyOutcome
{
    /// <summary>A binding completed; see <see cref="KeyResult.Action"/> and <see cref="KeyResult.Count"/>.</summary>
    Action,

    /// <summary>The key extended a chord or count; more keys are needed.</summary>
    Pending,

    /// <summary>Printable text in Insert or Command mode; see <see cref="KeyResult.Text"/>.</summary>
    TextInput,

    /// <summary>Esc dropped a pending chord or count.</summary>
    Cancelled,

    /// <summary>The key did not continue a pending chord; the whole chord was discarded and the key is consumed.</summary>
    Discarded,

    /// <summary>No binding matched and nothing was pending; the caller may pass the key on (for example to a text widget).</summary>
    Unbound,
}
