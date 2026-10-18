using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Overlays;

namespace Gitunia.Tui.App.Overlays;

/// <summary>The row an actions menu opens on: what it can do and where it is on screen.</summary>
/// <param name="Actions">The actions that take the row as their target.</param>
/// <param name="Anchor">The screen cell of the row.</param>
public sealed record RowMenuTarget(IReadOnlyList<ActionId> Actions, MenuAnchor Anchor);
