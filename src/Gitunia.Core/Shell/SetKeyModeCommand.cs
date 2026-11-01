using Gitunia.Core.Keymap.Models;

namespace Gitunia.Core.Shell;

/// <summary>Switch the keymap mode (Insert while a prompt takes typed text); a pending chord or count is dropped.</summary>
/// <param name="Mode">The mode to switch to.</param>
public sealed record SetKeyModeCommand(KeyMode Mode) : ShellCommand;
