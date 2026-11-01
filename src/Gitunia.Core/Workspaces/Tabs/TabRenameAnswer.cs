using Gitunia.Core.Prompts;

namespace Gitunia.Core.Workspaces.Tabs;

/// <summary>The result of the rename prompt: the typed text becomes <see cref="TabRenameAnswered"/> for the tab.</summary>
/// <param name="TabId">The tab that was being renamed.</param>
public sealed record TabRenameAnswer(TabId TabId) : IPromptResult
{
    public AppCommand Answered(PromptAnswer answer) => new TabRenameAnswered(TabId, answer.Text);
}
