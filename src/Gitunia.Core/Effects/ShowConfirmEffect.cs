using Gitunia.Core.Overlays;

namespace Gitunia.Core.Effects;

/// <summary>Ask the user to confirm; the answer <c>y</c> dispatches <paramref name="OnAccepted"/>, anything else
/// drops it.</summary>
/// <param name="Prompt">What the dialog says.</param>
/// <param name="OnAccepted">The command to dispatch on <c>y</c>; it names its target taken from what the user
/// saw.</param>
/// <param name="UnavailableNotice">The warning shown when the dialog cannot be displayed (the screen is too
/// small).</param>
public sealed record ShowConfirmEffect(ConfirmPrompt Prompt, AppCommand OnAccepted, string UnavailableNotice)
    : AppEffect;
