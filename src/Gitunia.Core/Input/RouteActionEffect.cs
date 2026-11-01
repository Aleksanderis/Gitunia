using System.Collections.Immutable;
using Gitunia.Core.Keymap.Models;

namespace Gitunia.Core.Input;

/// <summary>
/// Offer an action to these handlers, in this order; the first that takes it ends the run. The terminal layer performs
/// it (the handlers read the lists and views, which are not state).
/// </summary>
/// <param name="Action">The action.</param>
/// <param name="Count">The numeric prefix, null when none was typed.</param>
/// <param name="Handlers">Who may take it, in order; empty when nothing may.</param>
public sealed record RouteActionEffect(ActionId Action, int? Count, ImmutableArray<InputHandler> Handlers) : AppEffect;
