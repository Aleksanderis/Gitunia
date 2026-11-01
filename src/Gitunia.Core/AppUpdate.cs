using System.Collections.Immutable;

namespace Gitunia.Core;

/// <summary>The result of reducing a command: the new state and the effects for the runner.</summary>
/// <param name="State">The new state.</param>
/// <param name="Effects">Effects to run, possibly empty.</param>
public sealed record AppUpdate(AppState State, ImmutableArray<AppEffect> Effects);
