using Gitunia.Core;
using Gitunia.Core.Clipboard;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Overlays;
using Gitunia.Core.Workspaces.Tabs;

namespace Gitunia.App.Tests.Fakes;

/// <summary>A shell that records what the runner asked of the terminal layer; a confirm is answered by the test.</summary>
public sealed class RecordingShell : IShellEffects
{
    public List<KeyMode> Modes { get; } = [];

    public List<ConfirmRequest> Confirms { get; } = [];

    public List<string> Retries { get; } = [];

    /// <summary>Makes the next key mode switch throw, as a broken handler would.</summary>
    public bool ThrowOnKeyMode { get; set; }

    public bool ShowConfirm(ConfirmPrompt prompt, AppCommand onAccepted)
    {
        Confirms.Add(new ConfirmRequest(prompt, onAccepted));

        return true;
    }

    public void SetKeyMode(KeyMode mode)
    {
        if (ThrowOnKeyMode)
        {
            ThrowOnKeyMode = false;
            throw new InvalidOperationException("boom");
        }

        Modes.Add(mode);
    }

    public void CopyToClipboard(CopyToClipboardEffect effect)
    {
    }

    public void OfferTerminalRetry(TabId tabId, string noun, IReadOnlyList<string> arguments) =>
        Retries.Add($"{noun}|{string.Join(' ', arguments)}");

    public sealed record ConfirmRequest(ConfirmPrompt Prompt, AppCommand OnAccepted);
}
