using Gitunia.Core.Workspaces;

namespace Gitunia.Core.Clipboard;

/// <summary>Copy text to the system clipboard. The runner in the rendering layer writes the OSC 52 sequence and shows the outcome message.</summary>
/// <param name="Text">The text to copy.</param>
/// <param name="Description">What is copied, for the confirmation message (for example "commit hash").</param>
public sealed record CopyToClipboardEffect(string Text, string Description) : WorkspaceEffect;
