using Gitunia.App.Loading.Commits;
using Gitunia.App.Tabs;
using Gitunia.Core.DiffPane;
using Gitunia.Git;

namespace Gitunia.App.DiffPane;

/// <summary>
/// Starts the loads of the diff pane on the stores of the tab's session: the diff of a commit, the same diff with other
/// options or without its limits, and the content of a file. The stores read git off the UI thread and raise their own
/// events when they changed, so this runs on the UI thread, returns at once and posts no result command. It never reads
/// or changes state. A tab without a session (closed meanwhile) or without a store (tests) does nothing.
/// </summary>
/// <param name="sessions">The tab sessions.</param>
public sealed class DiffPaneEffectHandler(ITabSessions sessions)
{
    /// <summary>Starts the effect.</summary>
    /// <param name="effect">The effect.</param>
    public void Handle(DiffPaneEffect effect)
    {
        if (sessions.FindSession(effect.TabId) is not { } session)
        {
            return;
        }

        switch (effect)
        {
            case LoadDiffEffect load:
                LoadDiff(session, load);
                break;
            case SetDiffOptionsEffect options:
                session.DiffStore?.SetOptions(options.Options);
                break;
            case LoadWholeDiffEffect:
                session.DiffStore?.LoadAnyway();
                break;
            case ClearDiffEffect:
                session.DiffStore?.Clear();
                session.ContentStore?.Clear();
                break;
            case LoadFileContentEffect content when content.Row.Blob is { } blob:
                session.ContentStore?.Load(content.Commit, content.Row.Path, blob, content.Row.Size, content.Delay);
                break;
            case ClearFileContentEffect:
                session.ContentStore?.Clear();
                break;
            default:
                throw new UnhandledEffectException(effect);
        }
    }

    private static void LoadDiff(TabSession session, LoadDiffEffect load)
    {
        var target = load.Target;
        IReadOnlyList<ObjectId>? neighbours = null;
        if (load.Prefetch)
        {
            neighbours = DetailPrefetchPlan.Around(session.Graph, session.Graph.IndexOf(target.Commit));
        }

        session.DiffStore?.Load(target.Commit, target.Parent, neighbours, target.IsAlternateParent);
    }
}
