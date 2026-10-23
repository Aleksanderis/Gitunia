using Gitunia.Core.Workspaces.Repositories;

namespace Gitunia.Core.Workspaces.Tabs;

/// <summary>Switch the tab's file lists to the next path style: name first, full path, tree (not saved with the
/// session).</summary>
/// <param name="IncludesTree">Whether the tree style is part of the cycle; false in the working-tree view, whose lists
/// have none.</param>
public sealed record TogglePathStyleCommand(bool IncludesTree = true) : RepositoryCommand;
