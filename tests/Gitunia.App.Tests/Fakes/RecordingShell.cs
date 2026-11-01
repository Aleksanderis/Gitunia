using Gitunia.Core.Clipboard;
using Gitunia.Core.Workspaces.Tabs;

namespace Gitunia.App.Tests.Fakes;

/// <summary>A shell that records what the runner asked of the terminal layer.</summary>
public sealed class RecordingShell : IShellEffects
{
    public List<string> Retries { get; } = [];

    public void CopyToClipboard(CopyToClipboardEffect effect)
    {
    }

    public void OfferTerminalRetry(TabId tabId, string noun, IReadOnlyList<string> arguments) =>
        Retries.Add($"{noun}|{string.Join(' ', arguments)}");
}
