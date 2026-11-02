using Gitunia.Core.Conflicts;
using Gitunia.Core.FileTree;
using Gitunia.Core.Reflog;

namespace Gitunia.App;

/// <summary>Flows the Tui layer still owns (reflog entry, resolved conflict, file content).</summary>
public interface IFlowEffects
{
    /// <summary>
    /// Starts the flow a reflog entry stands for (the reset prompt, the branch prompt, a detached checkout, the graph
    /// cursor) and answers with <c>ReflogEntryActionStarted</c> or <c>ReflogEntryActionRefused</c>.
    /// </summary>
    void StartReflogEntryAction(ReflogEntryActionEffect effect);

    /// <summary>
    /// Marks a saved, fully resolved conflicted file as resolved (stages it) and reports.
    /// </summary>
    void StageResolvedConflict(StageResolvedConflictEffect effect);

    /// <summary>Shows the content of a file row of the Files tab in the diff pane's place; reports why it cannot.</summary>
    void OpenFileContent(FileTreeRow row);
}
