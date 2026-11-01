using Gitunia.Core.Prompts;
using Gitunia.Core.Workspaces.Tabs;

namespace Gitunia.Core.Tags;

/// <summary>The result of the new-tag name prompt.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="StartPoint">The commit the tag is made at, taken from what the user saw.</param>
/// <param name="Label">How the start point is named for the user.</param>
public sealed record TagNameAnswer(TabId TabId, string StartPoint, string Label) : IPromptResult
{
    public AppCommand Answered(PromptAnswer answer) => new TagNameAccepted(TabId, StartPoint, Label, answer.Text);
}

/// <summary>The result of the tag message prompt.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="StartPoint">The commit the tag is made at.</param>
/// <param name="Label">How the start point is named for the user.</param>
/// <param name="Name">The accepted name of the new tag.</param>
public sealed record TagMessageAnswer(TabId TabId, string StartPoint, string Label, string Name) : IPromptResult
{
    public AppCommand Answered(PromptAnswer answer) =>
        new TagMessageAccepted(TabId, StartPoint, Label, Name, answer.Text);
}

/// <summary>The result of the remote picker of a tag push or remote delete.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="TagName">The tag the prompt was opened for.</param>
/// <param name="Push">True to push, false to delete on the remote.</param>
public sealed record TagRemoteAnswer(TabId TabId, string TagName, bool Push) : IPromptResult
{
    public AppCommand Answered(PromptAnswer answer) =>
        new TagRemoteChosen(TabId, TagName, Push, answer.Choice ?? string.Empty);
}
