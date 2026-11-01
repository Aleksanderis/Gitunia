using Gitunia.App.Tabs;
using Gitunia.Core.Compare;

namespace Gitunia.App.Compare;

/// <summary>
/// Runs the comparison effects on the details store of the tab's session, on the UI thread. The store loads off the
/// thread and debounces itself, so this starts no process per call. It never reads or changes state.
/// </summary>
/// <param name="sessions">The sessions of the open tabs.</param>
/// <param name="dispatcher">Where the answer goes.</param>
public sealed class CompareEffectHandler(ITabSessions sessions, IDispatcher dispatcher)
{
    /// <summary>Performs the effect; <see cref="StartComparisonEffect"/> answers with <see cref="CompareStarted"/>.</summary>
    public void Handle(CompareEffect effect)
    {
        var session = sessions.Find(effect.TabId);
        switch (effect)
        {
            case ClearComparisonEffect:
                session?.Details.ClearComparison();
                break;
            case StartComparisonEffect start:
                if (session?.Details.Compare(start.Older, start.Newer) == true)
                {
                    dispatcher.Post(new CompareStarted(start.TabId, start.Selected));
                }

                break;
            default:
                throw new UnhandledEffectException(effect);
        }
    }
}
