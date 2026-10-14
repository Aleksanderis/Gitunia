namespace Gitunia.Tui.App.Clipboard;

/// <summary>The outcome of copying a Visual range.</summary>
/// <param name="Copied">The text reached the clipboard; false keeps the range so the user can retry.</param>
/// <param name="Notice">A notice for the status bar, empty when there is none.</param>
public sealed record RangeCopyResult(bool Copied, string Notice);
