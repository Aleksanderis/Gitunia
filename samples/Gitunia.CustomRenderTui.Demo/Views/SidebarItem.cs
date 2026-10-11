namespace Gitunia.CustomRenderTui.Demo.Views;

/// <summary>One sidebar row: a section header or a ref name.</summary>
public readonly record struct SidebarItem(string Text, bool Header);
