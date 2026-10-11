using Gitunia.CustomRenderTui.Demo.Graph;

namespace Gitunia.CustomRenderTui.Demo.Views;

/// <summary>Everything the screen shows, handed to <see cref="Screen.Draw"/> each frame.</summary>
public sealed class ScreenModel
{
    public string RepoName { get; init; } = string.Empty;

    public Pane Focus { get; init; }

    public CommitGraph Graph { get; init; } = new();

    public ListCursor GraphCursor { get; init; } = new();

    public IReadOnlyList<SidebarItem> Sidebar { get; init; } = [];

    public ListCursor SidebarCursor { get; init; } = new();

    public IReadOnlyList<string> Details { get; init; } = [];

    public ListCursor DetailsCursor { get; init; } = new();

    public bool Loading { get; init; }

    public string Status { get; init; } = string.Empty;
}
