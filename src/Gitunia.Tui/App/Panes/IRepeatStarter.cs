using Gitunia.Core.Keymap.Models;

namespace Gitunia.Tui.App.Panes;

/// <summary>Starts the sticky repeat mode after a pane took a step action.</summary>
public interface IRepeatStarter
{
    /// <summary>Starts the mode the action belongs to, unless the step did not take effect.</summary>
    /// <param name="action">The action a pane just took.</param>
    void AfterPaneAction(ActionId action);
}
