using System.Collections.Immutable;
using Gitunia.Core.Keymap.Models;

namespace Gitunia.Tui.Rendering.Panes;

/// <summary>One status bar hint: the keys and what they do.</summary>
/// <param name="Keys">The keys in the notation of <c>docs/ux.md</c> (<c>Ctrl-d</c>, <c>gg</c>).</param>
/// <param name="Description">What the keys do, never empty. Short on purpose, it does not come from the action titles.</param>
/// <param name="Actions">The action each slash-separated alternative in <paramref name="Keys"/> triggers, in order; a test checks the default keymap binds them so.</param>
public sealed record KeyHint(string Keys, string Description, ImmutableArray<ActionId> Actions);
