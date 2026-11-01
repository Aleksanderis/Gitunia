using Gitunia.App.Status;
using Gitunia.Core.Clipboard;

namespace Gitunia.Tui.App.Clipboard;

/// <summary>
/// Copies text for the shell: runs the effect through the <see cref="ClipboardWriter"/> and reports the outcome on the
/// status channel. The status bar clips long messages to the screen width, so the full text stays in the history. Call it on the UI thread so the OSC 52 write never interleaves with Terminal.Gui output.
/// </summary>
/// <param name="writer">The writer, or null when the console is not interactive (nothing is copied then).</param>
/// <param name="statuses">Where the confirmation or failure message goes.</param>
public sealed class ClipboardCopier(ClipboardWriter? writer, StatusReporter statuses)
{
    public const string NotInteractiveMessage = "Cannot copy: the console is not interactive.";

    /// <returns>True when the text was copied, false when a warning was posted instead.</returns>
    public bool Copy(string text, string description)
    {
        if (writer is null)
        {
            statuses.Warning(NotInteractiveMessage);
            return false;
        }

        var outcome = writer.Run(new CopyToClipboardEffect(text, description));
        var message = outcome.Message;
        if (outcome.Succeeded)
        {
            statuses.Post(message);
            return true;
        }
        else
        {
            statuses.Warning(message);
            return false;
        }
    }
}
