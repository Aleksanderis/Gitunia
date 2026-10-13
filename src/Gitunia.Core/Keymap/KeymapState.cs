using System.Collections.Immutable;

namespace Gitunia.Core.Keymap;

/// <summary>Immutable input state of one window: mode, keys of an unfinished chord and an unfinished count.</summary>
public sealed record KeymapState(KeyMode Mode, ImmutableArray<KeyEvent> Pending, int? Count)
{
    public static KeymapState Initial { get; } = new(KeyMode.Normal, [], null);

    /// <summary>True while a chord prefix is waiting for its next key (shown in the status bar).</summary>
    public bool HasPending => !Pending.IsDefaultOrEmpty;

    public KeySequence PendingSequence => new(Pending.IsDefault ? [] : Pending);

    /// <summary>Switches mode and drops any pending chord or count.</summary>
    public KeymapState WithMode(KeyMode mode) => this with { Mode = mode, Pending = [], Count = null };
}
