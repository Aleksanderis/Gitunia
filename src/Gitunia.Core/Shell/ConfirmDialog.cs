using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Overlays;

namespace Gitunia.Core.Shell;

/// <summary>An open confirm dialog: what it says, the command that runs on <c>y</c> and the mode to go back to.</summary>
/// <param name="Prompt">What the dialog says.</param>
/// <param name="OnAccepted">Reduced when the user answers <c>y</c>; dropped for anything else. It names its target
/// taken from what the user saw.</param>
/// <param name="ReturnMode">The key mode to restore when the dialog closes.</param>
public sealed record ConfirmDialog(ConfirmPrompt Prompt, AppCommand OnAccepted, KeyMode ReturnMode);
