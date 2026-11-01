using System.Collections.Immutable;

namespace Gitunia.Core.Shell;

/// <summary>The result of a shell command: the new state, the effects, and a command to reduce right after (the
/// accepted command of a confirm dialog, a warning).</summary>
/// <param name="State">The new shell state.</param>
/// <param name="Effects">Effects to run, possibly empty.</param>
/// <param name="Next">A command to reduce next on the root state, or null.</param>
public sealed record ShellUpdate(ShellState State, ImmutableArray<AppEffect> Effects, AppCommand? Next = null);
