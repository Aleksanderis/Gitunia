using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Repositories.Models;

namespace Gitunia.Core.Reflog;

/// <summary>Read the reflog of one ref. Posts <see cref="ReflogLoaded"/>, <see cref="ReflogReadFailed"/> or
/// <see cref="ReflogReadCancelled"/>.</summary>
/// <param name="TabId">The tab the list belongs to.</param>
/// <param name="Location">Where git runs.</param>
/// <param name="Reference">The ref as git is asked for it (<c>HEAD</c> or <c>refs/heads/main</c>).</param>
/// <param name="Title">The ref as the user sees it.</param>
/// <param name="Generation">The read number; the result echoes it.</param>
/// <param name="Refresh">True when the open list is only updated with fresh entries.</param>
public sealed record ReadReflogEffect(
    TabId TabId,
    RepositoryLocation Location,
    string Reference,
    string Title,
    long Generation,
    bool Refresh) : AppEffect;

/// <summary>
/// Start the flow an entry stands for. The flows live in the terminal layer (prompts, the graph cursor), so the shell
/// carries it out and answers with <see cref="ReflogEntryActionStarted"/> or <see cref="ReflogEntryActionRefused"/>.
/// </summary>
/// <param name="TabId">The tab the list belongs to.</param>
/// <param name="Kind">What to do with the entry.</param>
/// <param name="Hash">The entry's commit.</param>
public sealed record ReflogEntryActionEffect(TabId TabId, ReflogEntryKind Kind, string Hash) : AppEffect;
