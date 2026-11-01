using System.Collections.Immutable;

namespace Gitunia.Core.Tags;

/// <summary>The result of a tag command: the new slice and the effects to run.</summary>
/// <param name="State">The new tag state of the tab.</param>
/// <param name="Effects">Effects for the runner, possibly empty.</param>
public sealed record TagUpdate(TagState State, ImmutableArray<AppEffect> Effects);
