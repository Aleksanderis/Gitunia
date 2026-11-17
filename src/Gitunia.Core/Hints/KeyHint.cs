using System.Collections.Immutable;
using Gitunia.Core.Keymap.Models;

namespace Gitunia.Core.Hints;

/// <summary>
/// One key hint: the keys and what they do. Declared once in <see cref="HintSet"/>; the status bar, the footers of the
/// overlays and the action rows all draw from it, and a click on it runs its action.
/// </summary>
/// <param name="Keys">The keys in the notation of <c>docs/internal/ux-keymap.md</c> (<c>Ctrl-d</c>, <c>gg</c>).</param>
/// <param name="Description">
/// What the keys do, never empty. Short on purpose, it does not come from the action titles.
/// </param>
/// <param name="Actions">
/// The action each slash-separated alternative in <paramref name="Keys"/> triggers, in order; a test checks the default
/// keymap binds them so. A click on the key of an alternative runs its action, a click on the description the first.
/// </param>
/// <param name="Detail">
/// A longer sentence for the action rows of the screens that list their actions (the reflog and the blame view); null
/// for a hint that has no row.
/// </param>
public sealed record KeyHint(string Keys, string Description, ImmutableArray<ActionId> Actions, string? Detail = null);
