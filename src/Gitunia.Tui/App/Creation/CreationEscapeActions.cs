using Gitunia.Core.Keymap.Models;
using Gitunia.Tui.Input;

namespace Gitunia.Tui.App.Creation;

/// <summary>Esc cancels a running clone, once every other holder of Esc passed.</summary>
/// <param name="creation">The clone and init input.</param>
public sealed class CreationEscapeActions(CreationInput creation) : IActionHandler
{
    public bool TryExecute(ActionId action, int? count) => creation.TryCancelWithEscape(action);
}
