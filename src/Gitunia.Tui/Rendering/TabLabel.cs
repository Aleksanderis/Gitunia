namespace Gitunia.Tui.Rendering;

/// <summary>What the tab bar shows for one tab. Plain data, so the bar can be rendered without a terminal.</summary>
/// <param name="Number">One-based position of the tab (what <c>Alt-N</c> and a click jump to).</param>
/// <param name="Name">The repository name, with its parent directory when another tab has the same name.</param>
/// <param name="Branch">The checked-out branch, null when detached or not loaded yet.</param>
/// <param name="IsActive">The tab is the active one.</param>
/// <param name="IsDirty">The working tree has changes.</param>
/// <param name="Ahead">Commits ahead of the upstream.</param>
/// <param name="Behind">Commits behind the upstream.</param>
/// <param name="IsMissing">The repository directory is gone.</param>
/// <param name="IsLoading">The tab is still loading in the background.</param>
public sealed record TabLabel(
    int Number,
    string Name,
    string? Branch,
    bool IsActive,
    bool IsDirty,
    int Ahead,
    int Behind,
    bool IsMissing,
    bool IsLoading);
