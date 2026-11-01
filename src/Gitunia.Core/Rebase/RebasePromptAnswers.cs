using Gitunia.Core.Prompts;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Rebase;

namespace Gitunia.Core.Rebase;

/// <summary>The result of the reword subject prompt.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="Plan">The plan the prompt was opened for.</param>
public sealed record QuickRebaseSubjectAnswer(TabId TabId, QuickRebasePlan Plan) : IPromptResult
{
    public AppCommand Answered(PromptAnswer answer) => new QuickRebaseSubjectAccepted(TabId, Plan, answer.Text);
}
