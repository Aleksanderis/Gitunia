using System.Collections.Immutable;
using Gitunia.Core.Prompts;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Remotes.Models;

namespace Gitunia.Core.Remotes;

/// <summary>The result of the name prompt of an added remote.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="Remotes">The remotes read when the action started, to refuse a duplicate.</param>
public sealed record RemoteAddNameAnswer(TabId TabId, ImmutableArray<RemoteInfo> Remotes) : IPromptResult
{
    public AppCommand Answered(PromptAnswer answer) => new RemoteAddNameAccepted(TabId, Remotes, answer.Text);
}

/// <summary>The result of the URL prompt of an added remote.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="Name">The accepted name of the new remote.</param>
public sealed record RemoteAddUrlAnswer(TabId TabId, string Name) : IPromptResult
{
    public AppCommand Answered(PromptAnswer answer) => new RemoteAddUrlAccepted(TabId, Name, answer.Text);
}

/// <summary>The result of the remote picker.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="Action">What was asked for.</param>
/// <param name="Remotes">The remotes read when the action started.</param>
public sealed record RemotePickAnswer(TabId TabId, RemoteAction Action, ImmutableArray<RemoteInfo> Remotes)
    : IPromptResult
{
    public AppCommand Answered(PromptAnswer answer) =>
        new RemoteChosen(TabId, Action, Remotes, answer.Choice ?? string.Empty);
}

/// <summary>The result of the new-name prompt of a rename.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="Remotes">The remotes read when the action started, to refuse a duplicate.</param>
/// <param name="OldName">The remote being renamed.</param>
public sealed record RemoteRenameAnswer(TabId TabId, ImmutableArray<RemoteInfo> Remotes, string OldName)
    : IPromptResult
{
    public AppCommand Answered(PromptAnswer answer) => new RemoteRenameAccepted(TabId, Remotes, OldName, answer.Text);
}

/// <summary>The result of the URL prompt of a set-URL.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="Name">The remote.</param>
/// <param name="OldUrl">Its URL when the prompt opened.</param>
public sealed record RemoteUrlAnswer(TabId TabId, string Name, string OldUrl) : IPromptResult
{
    public AppCommand Answered(PromptAnswer answer) => new RemoteUrlAccepted(TabId, Name, OldUrl, answer.Text);
}
