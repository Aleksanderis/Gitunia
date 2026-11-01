using Gitunia.Core.Workspaces.Tabs;

namespace Gitunia.Core.Rebase;

/// <summary>
/// The interactive rebase editor as the shell state holds it: the open todo (null while closed) and the bookkeeping of
/// the reads that fill it. Changed only by <see cref="RebaseEditorReducer"/>.
/// </summary>
/// <param name="Editor">The open todo, null while the editor is closed.</param>
/// <param name="Generation">Counts reads and closes; a read result with another number is stale and dropped.</param>
/// <param name="IsReading">True from a read effect (the range, or the message of a reword) until its result arrives;
/// another read is refused meanwhile.</param>
public sealed record RebaseEditorSession(RebaseEditorState? Editor, long Generation, bool IsReading)
{
    public static RebaseEditorSession Empty { get; } = new(null, 0, false);

    public bool IsOpen => Editor is not null;

    /// <summary>True while the message editor of a reword is open.</summary>
    public bool IsEditingMessage => Editor?.Edit is not null;

    /// <summary>True when typed keys go to the message editor of the todo in the active tab.</summary>
    /// <param name="activeTab">The active tab.</param>
    public bool TakesText(TabId? activeTab) => Editor is { Edit: not null } state && activeTab == state.TabId;
}
