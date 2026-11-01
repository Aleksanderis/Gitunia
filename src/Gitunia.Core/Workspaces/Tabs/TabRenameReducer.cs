using Gitunia.Core.Effects;
using Gitunia.Core.Prompts;
using Gitunia.Core.Status;
using Gitunia.Core.Workspaces.Repositories;

namespace Gitunia.Core.Workspaces.Tabs;

/// <summary>
/// Renaming a tab as pure transitions <c>(RepositoryState, TabRenameCommand) -> TabRenameUpdate</c> (#219, #971): the
/// request opens a prompt that starts with the tab's current name, the answer stores the name in the tab and says what
/// the tab is called now. The first feature on the shared prompt model; it is the recipe in docs/architecture.md.
/// </summary>
public static class TabRenameReducer
{
    /// <summary>Shown above the field: what an empty name does.</summary>
    public const string PromptContext = "Empty restores the repository name";

    /// <summary>Applies a command to the tab it is for.</summary>
    public static TabRenameUpdate Reduce(RepositoryState tab, TabRenameCommand command) => command switch
    {
        TabRenameAnswered answered => Answered(tab, answered),
        TabRenameRequested => Requested(tab),
        _ => throw new UnhandledCommandException(command),
    };

    private static TabRenameUpdate Requested(RepositoryState tab)
    {
        var request = new PromptRequest(
            "Rename tab",
            PromptContext,
            "save",
            new TabRenameAnswer(tab.Id),
            tab.Title,
            Owner: tab.Id);

        return new TabRenameUpdate(tab, [new ShowPromptEffect(request)]);
    }

    private static TabRenameUpdate Answered(RepositoryState tab, TabRenameAnswered answered)
    {
        var renamed = RepositoryReducer.Reduce(tab, new SetTabNameCommand(answered.Name));

        return new TabRenameUpdate(renamed, [new PostStatusEffect($"tab is now {renamed.Title}", StatusSeverity.Info)]);
    }
}
