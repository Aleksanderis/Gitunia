using System.Collections.Immutable;

namespace Gitunia.Core.Sync;

/// <summary>What a sync command changes: the tab's running transfer and the effects to run.</summary>
/// <param name="State">The tab's running transfer after the command, null when none.</param>
/// <param name="Effects">The effects to run.</param>
public sealed record SyncUpdate(SyncState? State, ImmutableArray<AppEffect> Effects);
