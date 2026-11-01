using System.Collections.Immutable;

namespace Gitunia.Core.Rebase;

/// <summary>The result of an interactive rebase editor command.</summary>
/// <param name="Session">The new session.</param>
/// <param name="Effects">Effects to run.</param>
public sealed record RebaseEditorUpdate(RebaseEditorSession Session, ImmutableArray<AppEffect> Effects);
