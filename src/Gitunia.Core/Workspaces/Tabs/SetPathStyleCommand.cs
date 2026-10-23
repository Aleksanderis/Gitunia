using Gitunia.Core.Workspaces.Repositories;

namespace Gitunia.Core.Workspaces.Tabs;

/// <summary>Switch the tab's file lists to one path style (not saved with the session), from the <c>view ▾</c>
/// dropdown. It applies to the Changes tab and to both working-tree lists.</summary>
/// <param name="Style">The style to show.</param>
public sealed record SetPathStyleCommand(FilePathStyle Style) : RepositoryCommand;
