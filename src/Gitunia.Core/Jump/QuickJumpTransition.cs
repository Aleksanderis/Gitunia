using System.Collections.Immutable;

namespace Gitunia.Core.Jump;

/// <summary>The result of one quick jump command.</summary>
/// <param name="Session">The open prompt afterwards, null when closed.</param>
/// <param name="Effects">The effects to run.</param>
public sealed record QuickJumpTransition(QuickJumpSession? Session, ImmutableArray<AppEffect> Effects);
