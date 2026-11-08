using Gitunia.Core.Workspaces.Repositories;

namespace Gitunia.Core.FileHistory;

/// <summary>What the file history reducer needs to know about the tabs.</summary>
/// <param name="Tab">
/// The tab the command is about: the one an open request or a list result names, else the tab of the open history.
/// Null when it is gone.
/// </param>
public sealed record FileHistoryContext(RepositoryState? Tab);
