using Gitunia.Core.Overlays;

namespace Gitunia.Core.Shell;

/// <summary>
/// The overlay layer opened or closed the help, the palette, the actions menu or the message history, so the state
/// knows (a late question of a rebase must not open over them, #1020). The overlay itself stays with the overlay layer.
/// </summary>
/// <param name="Kind">The overlay that is open now, or <see cref="OverlayKind.None"/>.</param>
public sealed record OverlayChangedCommand(OverlayKind Kind) : ShellCommand;
