using Gitunia.Core.Workspaces.Repositories;

namespace Gitunia.Core.Workspaces.Tabs;

/// <summary>Switch the tab's file lists between the name-first and the full-path style (not saved with the
/// session).</summary>
public sealed record TogglePathStyleCommand : RepositoryCommand;
