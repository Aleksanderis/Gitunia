using Gitunia.Core.Status;
using System.Collections.Immutable;

using Gitunia.Core.Effects;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Git.ExternalTools;

namespace Gitunia.Core.ExternalTools;

/// <summary>
/// Maps a tool key to the hand-off for the active tab, as the pure transition
/// <c>(ExternalToolCommand, active tab) -> Effects</c>. The refusals that need no process are decided here.
/// </summary>
public static class ExternalToolReducer
{
    /// <summary>Shown when the key is pressed with no tab open.</summary>
    public const string NoRepositoryNotice = "No repository open";

    /// <summary>Shown when the difftool is asked for without a selected commit.</summary>
    public const string NoCommitNotice = "No commit selected";

    /// <summary>Applies a command; <paramref name="tab"/> is the active tab, null when none is open.</summary>
    public static ImmutableArray<AppEffect> Reduce(ExternalToolCommand command, RepositoryState? tab) => command switch
    {
        ExternalToolRequested request => Launch(request, tab),
        _ => throw new UnhandledCommandException(command),
    };

    private static ImmutableArray<AppEffect> Launch(ExternalToolRequested request, RepositoryState? tab)
    {
        if (tab is null)
        {
            return Notice(NoRepositoryNotice);
        }

        if (request.Kind == ExternalToolKind.Difftool && request.Commit is null)
        {
            return Notice(NoCommitNotice);
        }

        return [new LaunchExternalToolEffect(tab.Id, tab.Location, request.Kind, request.Commit)];
    }

    private static ImmutableArray<AppEffect> Notice(string text) =>
        [new PostStatusEffect(text, StatusSeverity.Info)];
}
