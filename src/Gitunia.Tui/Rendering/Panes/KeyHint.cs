namespace Gitunia.Tui.Rendering.Panes;

/// <summary>One status bar hint: the keys and what they do.</summary>
/// <param name="Keys">The keys in the notation of <c>docs/ux.md</c> (<c>Ctrl-d</c>, <c>gg</c>).</param>
/// <param name="Description">What the keys do, never empty.</param>
public sealed record KeyHint(string Keys, string Description);
