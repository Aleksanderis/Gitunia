using System.Collections.Immutable;

namespace Gitunia.Core.Reflog;

/// <summary>The result of a reflog command.</summary>
/// <param name="Session">The new session.</param>
/// <param name="Effects">Effects to run.</param>
public sealed record ReflogUpdate(ReflogSession Session, ImmutableArray<AppEffect> Effects);
