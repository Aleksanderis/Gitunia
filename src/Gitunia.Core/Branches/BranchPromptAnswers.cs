using Gitunia.Core.Prompts;
using Gitunia.Core.Workspaces.Tabs;

namespace Gitunia.Core.Branches;

/// <summary>The result of the new-branch name prompt.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="StartPoint">The ref or commit the branch starts at, taken from what the user saw.</param>
/// <param name="Label">How the start point is named for the user.</param>
/// <param name="Checkout">True to check the new branch out.</param>
public sealed record BranchNameAnswer(TabId TabId, string StartPoint, string Label, bool Checkout) : IPromptResult
{
    public AppCommand Answered(PromptAnswer answer) =>
        new BranchNameAccepted(TabId, StartPoint, Label, Checkout, answer.Text);
}

/// <summary>The result of the rename prompt.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="From">The branch the prompt was opened for.</param>
public sealed record BranchRenameAnswer(TabId TabId, string From) : IPromptResult
{
    public AppCommand Answered(PromptAnswer answer) => new BranchRenameAccepted(TabId, From, answer.Text);
}

/// <summary>The result of the switch picker.</summary>
/// <param name="TabId">The tab.</param>
public sealed record BranchSwitchAnswer(TabId TabId) : IPromptResult
{
    public AppCommand Answered(PromptAnswer answer) => new BranchSwitchChosen(TabId, answer.Choice ?? string.Empty);
}

/// <summary>The result of the delete list.</summary>
/// <param name="TabId">The tab.</param>
public sealed record BranchDeleteAnswer(TabId TabId) : IPromptResult
{
    public AppCommand Answered(PromptAnswer answer) => new BranchDeleteChosen(TabId, answer.Choice ?? string.Empty);
}

/// <summary>The result of the upstream picker.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="Branch">The local branch the prompt was opened for.</param>
public sealed record BranchUpstreamAnswer(TabId TabId, string Branch) : IPromptResult
{
    public AppCommand Answered(PromptAnswer answer) =>
        new BranchUpstreamChosen(TabId, Branch, answer.Choice ?? string.Empty);
}
