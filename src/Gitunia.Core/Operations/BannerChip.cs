using Gitunia.Core.Keymap.Models;

namespace Gitunia.Core.Operations;

/// <summary>One action the banner offers: its keys, its short name and the action a click or the keys run.</summary>
/// <param name="Keys">The keys in the notation of <c>docs/internal/ux-keymap.md</c>, such as <c>&lt;space&gt;gc</c>.</param>
/// <param name="Label">What the action does in one word.</param>
/// <param name="Action">The action; a test checks the default keymap binds <paramref name="Keys"/> to it.</param>
public sealed record BannerChip(string Keys, string Label, ActionId Action);
