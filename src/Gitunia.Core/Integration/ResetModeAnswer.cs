using Gitunia.Core.Prompts;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Reset;

namespace Gitunia.Core.Integration;

/// <summary>The result of the reset mode prompt: the key the user pressed picks the mode.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="CommitHash">The commit the branch moves to, fixed when the prompt opened.</param>
/// <param name="BranchName">The branch that moves.</param>
public sealed record ResetModeAnswer(TabId TabId, string CommitHash, string BranchName) : IPromptResult
{
    /// <summary>The <see cref="PromptKeyChoice.Value"/> of the soft option.</summary>
    public const string Soft = "soft";

    /// <summary>The <see cref="PromptKeyChoice.Value"/> of the mixed option.</summary>
    public const string Mixed = "mixed";

    /// <summary>The <see cref="PromptKeyChoice.Value"/> of the hard option.</summary>
    public const string Hard = "hard";

    public AppCommand Answered(PromptAnswer answer)
    {
        var mode = answer.Choice switch
        {
            Soft => ResetMode.Soft,
            Hard => ResetMode.Hard,
            _ => ResetMode.Mixed,
        };

        return new ResetModeChosen(TabId, new ResetTarget(CommitHash, BranchName, mode));
    }
}
