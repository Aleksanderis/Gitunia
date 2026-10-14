using Gitunia.Core.Workspaces.Repositories;

namespace Gitunia.Core.Workspaces.Tabs;

/// <summary>Rename the tab (stored in the session); a blank name goes back to the repository name.</summary>
/// <param name="Name">The new name; surrounding blanks are dropped.</param>
public sealed record SetTabNameCommand(string? Name) : RepositoryCommand;
