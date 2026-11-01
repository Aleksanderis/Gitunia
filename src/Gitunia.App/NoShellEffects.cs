using Gitunia.Core.Clipboard;
using Gitunia.Core.Config.Models;
using Gitunia.Core.Reflog;
using Gitunia.Core.Workspaces.Tabs;

namespace Gitunia.App;

/// <summary>The shell of a runner without a terminal: nothing happens.</summary>
public sealed class NoShellEffects : IShellEffects
{
    public static NoShellEffects Instance { get; } = new();

    public void CopyToClipboard(CopyToClipboardEffect effect)
    {
    }

    public void OfferTerminalRetry(TabId tabId, string noun, IReadOnlyList<string> arguments)
    {
    }

    public void StartReflogEntryAction(ReflogEntryActionEffect effect)
    {
    }

    public void ApplyOptions(ConfigOptions options)
    {
    }

    public void ApplyTheme(ThemeChoice theme)
    {
    }

    public void OpenConfigEditor()
    {
    }

    public bool MoveCommitDraft(TabId missing, TabId target) => true;

    public bool FlushSession() => true;

    public void Quit()
    {
    }
}
