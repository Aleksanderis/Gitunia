namespace Gitunia.Core.Editing;

/// <summary>
/// What a host of a text editor offers, declared once per host so the hosts stop drifting (see <see cref="EditorHosts"/>).
/// </summary>
/// <param name="HasUndo">Ctrl-z and Ctrl-y step through the editor's own undo points.</param>
/// <param name="HasClipboard">Copy (Alt-w) and cut (Ctrl-x) hand the selection to the clipboard.</param>
/// <param name="Escape">What Esc does with the typed text.</param>
public sealed record EditorHostPolicy(bool HasUndo, bool HasClipboard, DraftPolicy Escape);
