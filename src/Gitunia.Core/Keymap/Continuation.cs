namespace Gitunia.Core.Keymap;

/// <summary>One possible next key after a pending prefix, for the which-key popup.</summary>
/// <param name="Key">The next key.</param>
/// <param name="Action">The action this key completes, or null when it only extends the chord.</param>
/// <param name="HasMore">True when longer chords continue past this key.</param>
public sealed record Continuation(KeyEvent Key, ActionId? Action, bool HasMore);
