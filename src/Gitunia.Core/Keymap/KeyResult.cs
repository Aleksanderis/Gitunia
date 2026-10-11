namespace Gitunia.Core.Keymap;

/// <summary>What one key press did: the next state plus the outcome and its payload.</summary>
/// <param name="State">State to keep for the next key.</param>
/// <param name="Outcome">What happened.</param>
/// <param name="Action">The completed action for <see cref="KeyOutcome.Action"/>.</param>
/// <param name="Count">The numeric prefix typed before the binding (<c>5</c> in <c>5j</c>), or null when none.</param>
/// <param name="Text">The typed text for <see cref="KeyOutcome.TextInput"/>.</param>
public sealed record KeyResult(KeymapState State, KeyOutcome Outcome, ActionId? Action = null, int? Count = null, string? Text = null);
