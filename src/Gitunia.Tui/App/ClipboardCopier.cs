using Gitunia.Core.Clipboard;
using Gitunia.Core.Status;

namespace Gitunia.Tui.App;

/// <summary>
/// Copies text for the shell: runs the effect through the <see cref="ClipboardWriter"/> and reports the outcome on the
/// status channel. Call it on the UI thread so the OSC 52 write never interleaves with Terminal.Gui output.
/// </summary>
/// <param name="writer">The writer, or null when the console is not interactive (nothing is copied then).</param>
/// <param name="statuses">Where the confirmation or failure message goes.</param>
/// <param name="availableWidth">Columns available for the status message; longer messages are truncated.</param>
public sealed class ClipboardCopier(ClipboardWriter? writer, StatusChannel statuses, Func<int> availableWidth)
{
    public const string NotInteractiveMessage = "Cannot copy: the console is not interactive.";

    public void Copy(string text, string description)
    {
        if (writer is null)
        {
            statuses.Warning(NotInteractiveMessage);
            return;
        }

        var outcome = writer.Run(new CopyToClipboardEffect(text, description));
        var message = Truncate(outcome.Message, availableWidth());
        if (outcome.Succeeded)
        {
            statuses.Post(message);
        }
        else
        {
            statuses.Warning(message);
        }
    }

    private static string Truncate(string message, int width)
    {
        if (width <= 1 || message.Length <= width)
        {
            return message;
        }

        return string.Concat(message.AsSpan(0, width - 1), "…");
    }
}
