using Gitunia.Core.Prompts;
using Gitunia.Core.Workspaces.Tabs;

namespace Gitunia.Core.Stash;

/// <summary>The result of the stash message prompt.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="IncludeUntracked">Also stash the untracked files, fixed when the prompt opened.</param>
public sealed record StashMessageAnswer(TabId TabId, bool IncludeUntracked) : IPromptResult
{
    public AppCommand Answered(PromptAnswer answer) => new StashMessageAccepted(TabId, IncludeUntracked, answer.Text);
}
