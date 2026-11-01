using System.Collections.Immutable;

namespace Gitunia.Core.Conflicts;

/// <summary>The result of a conflict editor command.</summary>
/// <param name="Session">The new session.</param>
/// <param name="Effects">Effects to run.</param>
public sealed record ConflictUpdate(ConflictSession Session, ImmutableArray<AppEffect> Effects);
