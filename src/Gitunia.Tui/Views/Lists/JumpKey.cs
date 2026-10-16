using Gitunia.Core.Jump.Models;

namespace Gitunia.Tui.Views.Lists;

/// <summary>What the quick-jump labels of a frame were worked out from; the labels are reused while it stays the same.</summary>
/// <param name="Text">The typed text.</param>
/// <param name="Window">The rows in view and the cursor.</param>
/// <param name="Stamp">Tells the rows apart: the layout width, and whatever changes the rows (more commits, another document).</param>
public readonly record struct JumpKey(string Text, JumpWindow Window, int Stamp);
