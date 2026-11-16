using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;

namespace Gitunia.Tui.App.WorkingTree;

/// <summary>
/// Which actions the Changes view lets through to the next handler when it has no use for them. An action bound in the
/// Global view of the default keymap (in any mode) works in every view, so it falls through unless it is on
/// <see cref="Blocked"/>. Every other action (those of a list, the graph or the sidebar, and ones with no key at all)
/// stays swallowed, because the next handlers would apply them to the graph or sidebar hidden behind the Changes view.
/// A new Global action therefore works here without a change.
/// </summary>
public static class ChangesViewFallthrough
{
    private static readonly Lazy<HashSet<ActionId>> GlobalActions = new(LoadGlobalActions);

    /// <summary>Global actions the Changes view must still swallow, each with the reason.</summary>
    public static IReadOnlyDictionary<ActionId, string> Blocked { get; } = new Dictionary<ActionId, string>
    {
        [ActionIds.PaneZoom] =
            "DiffPaneInput zooms the diff of the history view, which is hidden here; the zoom of the Changes diff "
            + "is taken by WorkingDiffController before this point",
    };

    /// <summary>The actions bound in the Global view of the default keymap.</summary>
    public static IReadOnlySet<ActionId> Globals => GlobalActions.Value;

    /// <summary>True when the Changes view passes the action on instead of swallowing it.</summary>
    /// <param name="action">The action.</param>
    public static bool Applies(ActionId action) => GlobalActions.Value.Contains(action) && !Blocked.ContainsKey(action);

    private static HashSet<ActionId> LoadGlobalActions()
    {
        var actions = new HashSet<ActionId>();
        foreach (var binding in DefaultKeymap.CreateBindings())
        {
            if (binding.View == KeyBindingTable.GlobalView)
            {
                actions.Add(binding.Action);
            }
        }

        return actions;
    }
}
