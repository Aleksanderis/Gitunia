using Gitunia.Core.Prompts;
using Gitunia.Core.Workspaces.Tabs;

namespace Gitunia.Core.Worktrees;

/// <summary>The result of the branch prompt of a new worktree.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="SelectedStart">The commit a new branch starts at when one is typed, fixed when the prompt opened.</param>
public sealed record WorktreeBranchAnswer(TabId TabId, string? SelectedStart) : IPromptResult
{
    public AppCommand Answered(PromptAnswer answer) => new WorktreeBranchAccepted(TabId, SelectedStart, answer.Text);
}

/// <summary>The result of the path prompt of a new worktree.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="Branch">The branch the worktree gets.</param>
/// <param name="StartPoint">The commit a new branch starts at, null for an existing branch.</param>
/// <param name="OpenAfter">True to open the new worktree as its own tab once it exists.</param>
public sealed record WorktreePathAnswer(TabId TabId, string Branch, string? StartPoint, bool OpenAfter) : IPromptResult
{
    public AppCommand Answered(PromptAnswer answer) =>
        new WorktreePathAccepted(TabId, Branch, StartPoint, OpenAfter, answer.Text);
}

/// <summary>The result of the lock reason prompt.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="Path">The worktree the prompt was opened for.</param>
public sealed record WorktreeLockAnswer(TabId TabId, string Path) : IPromptResult
{
    public AppCommand Answered(PromptAnswer answer) => new WorktreeLockReasonAccepted(TabId, Path, answer.Text);
}
