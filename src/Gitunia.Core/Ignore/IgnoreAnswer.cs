using Gitunia.Core.Prompts;
using Gitunia.Core.Workspaces.Tabs;

namespace Gitunia.Core.Ignore;

/// <summary>The result of the pattern prompt: the key the user pressed picks the pattern, whose line is the
/// choice.</summary>
/// <param name="TabId">The tab.</param>
public sealed record IgnoreAnswer(TabId TabId) : IPromptResult
{
    /// <inheritdoc />
    public AppCommand Answered(PromptAnswer answer) => new IgnoreChosen(TabId, answer.Choice ?? string.Empty);
}
