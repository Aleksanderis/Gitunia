using Gitunia.Core.Diff;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.History.Models;

namespace Gitunia.Core.FileHistory;

/// <summary>
/// The open file history: whose file, its commits (newest first), the highlighted one and the diff of the file in
/// that commit. Pure. The lists are bounded (<see cref="FileHistoryReducer.EntryLimit"/> commits, one file's diff), so
/// like the reflog they stay in the state.
/// </summary>
/// <param name="TabId">The tab the history was opened in; the jump to the graph acts on that tab.</param>
/// <param name="Path">The path the file has at <paramref name="Revision"/>.</param>
/// <param name="Revision">Where the history starts (a commit hash, or <c>HEAD</c>).</param>
/// <param name="Entries">Every commit read.</param>
/// <param name="SelectedIndex">Index into <paramref name="Entries"/>.</param>
/// <param name="Diff">The diff of the highlighted commit, null while it is read or when there is no commit.</param>
/// <param name="DiffFailure">Why the diff could not be read, empty when it could.</param>
/// <param name="DiffScroll">The first diff row shown.</param>
/// <param name="Range">The lines the history follows, null for the whole file.</param>
public sealed record FileHistoryState(
    TabId TabId,
    string Path,
    string Revision,
    IReadOnlyList<FileHistoryEntry> Entries,
    int SelectedIndex = 0,
    DiffDocument? Diff = null,
    string DiffFailure = "",
    int DiffScroll = 0,
    LineRange? Range = null)
{
    /// <summary>The highlighted commit, null when the history is empty.</summary>
    public FileHistoryEntry? Selected =>
        Entries.Count == 0 ? null : Entries[Math.Clamp(SelectedIndex, 0, Entries.Count - 1)];

    /// <summary>Moves the highlight to <paramref name="index"/>, clamped to the entries; the diff is dropped.</summary>
    public FileHistoryState MoveTo(int index) =>
        this with
        {
            SelectedIndex = Entries.Count == 0 ? 0 : Math.Clamp(index, 0, Entries.Count - 1),
            Diff = null,
            DiffFailure = string.Empty,
            DiffScroll = 0,
        };

    /// <summary>The diff scrolled by <paramref name="rows"/>, kept inside the document.</summary>
    public FileHistoryState ScrollBy(int rows)
    {
        var last = Math.Max(0, (Diff?.Rows.Count ?? 0) - 1);

        return this with { DiffScroll = Math.Clamp(DiffScroll + rows, 0, last) };
    }
}
