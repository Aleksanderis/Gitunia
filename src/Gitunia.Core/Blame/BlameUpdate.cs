using System.Collections.Immutable;

namespace Gitunia.Core.Blame;

/// <summary>The result of a blame command.</summary>
/// <param name="Session">The new session.</param>
/// <param name="Effects">Effects to run.</param>
public sealed record BlameUpdate(BlameSession Session, ImmutableArray<AppEffect> Effects);
