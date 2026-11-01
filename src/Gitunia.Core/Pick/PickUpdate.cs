using System.Collections.Immutable;

namespace Gitunia.Core.Pick;

/// <summary>What the pick reducer returns: the mainline question that is open afterwards, and the effects.</summary>
/// <param name="Question">The open mainline question afterwards, null when none.</param>
/// <param name="Effects">The effects to run.</param>
public sealed record PickUpdate(MainlineQuestion? Question, ImmutableArray<AppEffect> Effects);
