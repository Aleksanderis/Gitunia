using Gitunia.Core.Workspaces.Repositories;

namespace Gitunia.Core.Workspaces.Tabs;

/// <summary>Switch the tab's file lists to the next path style: name first, full path, tree (not saved with the
/// session). It applies to the Changes tab and to both working-tree lists.</summary>
public sealed record TogglePathStyleCommand : RepositoryCommand;
