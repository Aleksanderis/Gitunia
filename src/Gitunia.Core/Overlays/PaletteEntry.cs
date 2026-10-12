using Gitunia.Core.Keymap;

namespace Gitunia.Core.Overlays;

/// <summary>One row of the command palette.</summary>
/// <param name="Action">The action it runs.</param>
/// <param name="Title">Human-readable title.</param>
/// <param name="Keys">Its bindings as shown to the user (<c>j / Down</c>), empty when it has none.</param>
public sealed record PaletteEntry(ActionId Action, string Title, string Keys);
