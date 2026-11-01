using Gitunia.Core.Overlays;

namespace Gitunia.Core.Shell;

/// <summary>The user answered the open confirm dialog (<c>y</c> is yes; <c>n</c> and <c>Esc</c> are no).</summary>
/// <param name="Answer">The answer.</param>
public sealed record ConfirmAnsweredCommand(ConfirmAnswer Answer) : ShellCommand;
