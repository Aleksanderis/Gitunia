using Gitunia.Core.Workspaces.Repositories;

namespace Gitunia.Core.Details;

/// <summary>Replace the tab's file tree state (filter, opened directories, closed folders).</summary>
/// <param name="Tree">The new state.</param>
public sealed record SetDetailTreeCommand(DetailTreeState Tree) : RepositoryCommand;
