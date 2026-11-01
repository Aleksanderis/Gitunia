using System.Collections.Immutable;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Shell;

namespace Gitunia.Core.Commits;

/// <summary>The result of a commit command.</summary>
/// <param name="State">The new state of the tab's message box.</param>
/// <param name="Mode">The key mode to switch to, null to keep it.</param>
/// <param name="Output">The output overlay to open, null for none.</param>
/// <param name="Effects">Effects to run.</param>
/// <param name="Next">Commands to reduce right after, in order.</param>
public sealed record CommitUpdate(
    CommitState State,
    KeyMode? Mode,
    OutputShownCommand? Output,
    ImmutableArray<AppEffect> Effects,
    ImmutableArray<AppCommand> Next);
