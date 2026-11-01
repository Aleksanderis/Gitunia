using System.Collections.Immutable;

namespace Gitunia.Core.Picker;

/// <summary>The result of a picker command: the new session, the effects, and the commands to reduce next on the root
/// state, in order (workspace commands, then the follow-up picker commands that see the new workspace).</summary>
/// <param name="Session">The new session.</param>
/// <param name="Effects">Effects to run.</param>
/// <param name="Next">Commands to reduce next.</param>
public sealed record PickerUpdate(PickerSession Session, ImmutableArray<AppEffect> Effects, ImmutableArray<AppCommand> Next);
