using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Reflog.Models;

namespace Gitunia.Core.Reflog;

/// <summary>The open reflog list: whose reflog, its entries (newest first) and the highlighted row. Pure.</summary>
/// <param name="TabId">The tab the list was opened in; the actions on an entry run in that tab.</param>
/// <param name="ReferenceName">The ref as git is asked for it (<c>HEAD</c> or <c>refs/heads/main</c>).</param>
/// <param name="Title">The ref as the user sees it (<c>HEAD</c> or <c>main</c>), the prefix of <c>main@{n}</c>.</param>
/// <param name="Entries">Every entry read.</param>
/// <param name="SelectedIndex">Index into <paramref name="Entries"/>.</param>
public sealed record ReflogState(
    TabId TabId,
    string ReferenceName,
    string Title,
    IReadOnlyList<ReflogEntry> Entries,
    int SelectedIndex = 0)
{
    /// <summary>The highlighted entry, null when the reflog is empty.</summary>
    public ReflogEntry? Selected =>
        Entries.Count == 0 ? null : Entries[Math.Clamp(SelectedIndex, 0, Entries.Count - 1)];

    /// <summary>Moves the highlight to <paramref name="index"/>, clamped to the entries.</summary>
    public ReflogState MoveTo(int index) =>
        this with { SelectedIndex = Entries.Count == 0 ? 0 : Math.Clamp(index, 0, Entries.Count - 1) };

    /// <summary>The state for a fresh read of the same reflog: the highlight stays on the same row index.</summary>
    public ReflogState WithEntries(IReadOnlyList<ReflogEntry> entries) =>
        this with
        {
            Entries = entries,
            SelectedIndex = entries.Count == 0 ? 0 : Math.Min(SelectedIndex, entries.Count - 1),
        };
}
