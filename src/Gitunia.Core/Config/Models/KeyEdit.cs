using Gitunia.Core.Keymap.Models;

namespace Gitunia.Core.Config.Models;

/// <summary>One validated config entry: bind the slot to the action, or unbind it when the action is null.</summary>
/// <param name="Slot">Mode, view and key sequence the entry is about.</param>
/// <param name="Action">The action to bind; null to unbind.</param>
public sealed record KeyEdit(BindingSlot Slot, ActionId? Action);
