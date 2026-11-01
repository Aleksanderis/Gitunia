using Gitunia.Tui.Rendering.Graph;
using Gitunia.Tui.Rendering.Styling;
using Gitunia.Tui.Views.Lists;

namespace Gitunia.Tui.Views;

/// <summary>What the list widgets are built with.</summary>
/// <param name="Glyphs">The characters the lists draw with.</param>
/// <param name="Keys">The keys the lists show on their buttons and group rows.</param>
/// <param name="SidebarSource">The rows of the sidebar.</param>
/// <param name="MessageBox">What the commit message box reads to draw.</param>
public sealed record PaneViewContent(
    GraphGlyphSet Glyphs,
    ChipKeys Keys,
    SidebarListSource SidebarSource,
    IMessageBoxPort MessageBox);
