using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.History.Models;
using Gitunia.Git.Repositories.Models;

namespace Gitunia.Core.FileHistory;

/// <summary>Read the commits that touched a file. Posts <see cref="FileHistoryLoaded"/>,
/// <see cref="FileHistoryReadFailed"/> or <see cref="FileHistoryReadCancelled"/>.</summary>
/// <param name="TabId">The tab the history belongs to.</param>
/// <param name="Location">Where the commands run.</param>
/// <param name="Revision">Where the walk starts.</param>
/// <param name="Path">The file.</param>
/// <param name="Generation">The read number; the result echoes it.</param>
public sealed record ReadFileHistoryEffect(
    TabId TabId,
    RepositoryLocation Location,
    string Revision,
    string Path,
    long Generation) : AppEffect;

/// <summary>Read one commit's diff of the file. Posts <see cref="FileHistoryDiffLoaded"/>,
/// <see cref="FileHistoryDiffFailed"/> or <see cref="FileHistoryDiffCancelled"/>; a newer request cancels the
/// older one.</summary>
/// <param name="TabId">The tab the history belongs to.</param>
/// <param name="Location">Where the commands run.</param>
/// <param name="Entry">The commit and the file's name in it.</param>
/// <param name="Generation">The diff read number; the result echoes it.</param>
public sealed record ReadFileHistoryDiffEffect(
    TabId TabId,
    RepositoryLocation Location,
    FileHistoryEntry Entry,
    long Generation) : AppEffect;

/// <summary>
/// Move the graph cursor of the tab to the commit. The graph cursor belongs to the terminal layer, so the shell
/// carries it out and answers with <see cref="FileHistoryJumpDone"/> or <see cref="FileHistoryJumpRefused"/>.
/// </summary>
/// <param name="TabId">The tab the history belongs to.</param>
/// <param name="Hash">The commit.</param>
public sealed record FileHistoryJumpEffect(TabId TabId, string Hash) : AppEffect;
