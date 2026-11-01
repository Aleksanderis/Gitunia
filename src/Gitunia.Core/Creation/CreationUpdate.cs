using System.Collections.Immutable;

namespace Gitunia.Core.Creation;

/// <summary>The result of a creation command.</summary>
/// <param name="Session">The new session.</param>
/// <param name="Effects">Effects to run.</param>
/// <param name="Next">Commands to reduce next on the root state.</param>
public sealed record CreationUpdate(
    CreationSession Session,
    ImmutableArray<AppEffect> Effects,
    ImmutableArray<AppCommand> Next);
