using Gitunia.Core.Clipboard;
using Gitunia.Core.Config.Models;
using Gitunia.Core.Conflicts;
using Gitunia.Core.Reflog;
using Gitunia.Core.Workspaces.Tabs;

namespace Gitunia.App.Tests.Fakes;

/// <summary>A shell that records what the runner asked of the terminal layer.</summary>
public sealed class RecordingShell : IShellEffects
{
    public List<string> Retries { get; } = [];

    public List<ReflogEntryActionEffect> EntryActions { get; } = [];

    public List<StageResolvedConflictEffect> StagedConflicts { get; } = [];

    public List<ConfigOptions> AppliedOptions { get; } = [];

    public List<ThemeChoice> AppliedThemes { get; } = [];

    public int EditorOpened { get; private set; }

    public void CopyToClipboard(CopyToClipboardEffect effect)
    {
    }

    public void OfferTerminalRetry(TabId tabId, string noun, IReadOnlyList<string> arguments) =>
        Retries.Add($"{noun}|{string.Join(' ', arguments)}");

    public void StartReflogEntryAction(ReflogEntryActionEffect effect) => EntryActions.Add(effect);

    public void StageResolvedConflict(StageResolvedConflictEffect effect) => StagedConflicts.Add(effect);

    public void ApplyOptions(ConfigOptions options) => AppliedOptions.Add(options);

    public void ApplyTheme(ThemeChoice theme) => AppliedThemes.Add(theme);

    public void OpenConfigEditor() => EditorOpened++;
}
