namespace Gitunia.Core.Workspaces;

/// <summary>Rename the tab (stored in the session); a blank name goes back to the repository name.</summary>
/// <param name="Name">The new name; surrounding blanks are dropped.</param>
public sealed record SetTabNameCommand(string? Name) : RepoCommand;
