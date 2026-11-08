using System.Collections.Immutable;

namespace Gitunia.Core.FileHistory;

/// <summary>The result of a file history command.</summary>
/// <param name="Session">The new session.</param>
/// <param name="Effects">Effects to run.</param>
public sealed record FileHistoryUpdate(FileHistorySession Session, ImmutableArray<AppEffect> Effects);
