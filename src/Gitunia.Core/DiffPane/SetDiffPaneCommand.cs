using Gitunia.Core.Workspaces.Repositories;

namespace Gitunia.Core.DiffPane;

/// <summary>Replace the tab's diff pane state.</summary>
/// <param name="Pane">The new state.</param>
public sealed record SetDiffPaneCommand(DiffPaneState Pane) : RepositoryCommand;
