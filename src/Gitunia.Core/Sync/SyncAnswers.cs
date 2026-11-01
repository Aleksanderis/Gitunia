using System.Collections.Immutable;
using Gitunia.Core.Prompts;
using Gitunia.Core.Workspaces.Tabs;

namespace Gitunia.Core.Sync;

/// <summary>The result of the remote picker of a first push.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="Branch">The branch the prompt was opened for.</param>
public sealed record SyncRemoteAnswer(TabId TabId, string Branch) : IPromptResult
{
    public AppCommand Answered(PromptAnswer answer) => new SyncRemoteChosen(TabId, Branch, answer.Choice ?? string.Empty);
}

/// <summary>The result of the remote picker of a remote branch delete.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="Branches">The remote branches known when the action started.</param>
public sealed record SyncDeleteRemoteAnswer(TabId TabId, ImmutableArray<RemoteBranchRef> Branches) : IPromptResult
{
    public AppCommand Answered(PromptAnswer answer) =>
        new SyncDeleteRemoteChosen(TabId, Branches, answer.Choice ?? string.Empty);
}

/// <summary>The result of the branch picker of a remote branch delete.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="Branches">The remote branches known when the action started.</param>
/// <param name="Remote">The remote chosen before.</param>
public sealed record SyncDeleteBranchAnswer(TabId TabId, ImmutableArray<RemoteBranchRef> Branches, string Remote)
    : IPromptResult
{
    public AppCommand Answered(PromptAnswer answer) =>
        new SyncDeleteBranchChosen(TabId, Branches, Remote, answer.Choice ?? string.Empty);
}
