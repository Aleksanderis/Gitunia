using Gitunia.Core.Workspaces.Tabs;

namespace Gitunia.Tui.Rendering.Details;

/// <summary>A bottom-pane tab before it is placed on the title row.</summary>
/// <param name="Tab">The tab it selects.</param>
/// <param name="Name">The label text.</param>
/// <param name="View">The mouse view name that identifies the button.</param>
public readonly record struct DetailTabSpecification(BottomTab Tab, string Name, string View);
