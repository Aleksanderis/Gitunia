namespace Gitunia.Core.Keymap.Models;

/// <summary>
/// One row of <c>keymap.default.json</c>: the keys (main key first, then aliases) that trigger an action in the
/// modes of a view.
/// </summary>
/// <param name="Action">The action to run.</param>
/// <param name="View">View name such as <c>Graph</c>, or <c>Global</c> for every view.</param>
/// <param name="Modes">Modes in which the keys are active.</param>
/// <param name="Keys">Key sequences in the notation of <see cref="KeySequenceParser"/>.</param>
public sealed record KeymapRow(ActionId Action, string View, IReadOnlyList<KeyMode> Modes, IReadOnlyList<string> Keys);
