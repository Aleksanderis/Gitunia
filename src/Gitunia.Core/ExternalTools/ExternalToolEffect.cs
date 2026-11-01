using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.ExternalTools;
using Gitunia.Git.Repositories.Models;

namespace Gitunia.Core.ExternalTools;

/// <summary>
/// Hand the terminal to a git tool for a tab's repository. The terminal layer suspends the UI, runs it and asks for
/// a refresh when it returns, so it is not a background operation of the tab.
/// </summary>
public sealed record LaunchExternalToolEffect(
    TabId TabId,
    RepositoryLocation Location,
    ExternalToolKind Kind,
    string? Commit) : AppEffect;
