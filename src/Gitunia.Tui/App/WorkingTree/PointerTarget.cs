using Gitunia.Core.Workspaces.Tabs;

namespace Gitunia.Tui.App.WorkingTree;

/// <summary>Where a pointer cell is in a tab's message: the line and display column.</summary>
/// <param name="Tab">The tab.</param>
/// <param name="Line">Zero-based line.</param>
/// <param name="Column">Display column, sideways scroll included.</param>
public readonly record struct PointerTarget(TabId Tab, int Line, int Column);
