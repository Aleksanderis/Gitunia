using Gitunia.Core.Overlays;

namespace Gitunia.Core.Shell;

/// <summary>
/// Ask the user to confirm. When the dialog fits the screen it opens (Confirm mode) and the answer <c>y</c> reduces
/// <paramref name="OnAccepted"/>; when it does not fit nothing opens, the action must not start and the optional
/// <paramref name="Refusal"/> tells why.
/// </summary>
/// <param name="Prompt">What the dialog says.</param>
/// <param name="OnAccepted">The command to reduce on <c>y</c>; it names its target taken from what the user saw.</param>
/// <param name="Refusal">The message to post when the dialog does not fit; null for none (the caller reports it).</param>
public sealed record ConfirmRequestedCommand(ConfirmPrompt Prompt, AppCommand OnAccepted, StatusPostedCommand? Refusal)
    : ShellCommand;
