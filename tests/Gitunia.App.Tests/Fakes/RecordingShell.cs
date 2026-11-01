using Gitunia.Core.Clipboard;
using Gitunia.Core.Config.Models;
using Gitunia.Core.Conflicts;
using Gitunia.Core.FileTree;
using Gitunia.Core.ExternalTools;
using Gitunia.Core.Picker;
using Gitunia.Core.Reflog;
using Gitunia.Core.Staging;
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

    public List<LaunchExternalToolEffect> Tools { get; } = [];

    public List<MoveCommitDraftEffect> DraftMoves { get; } = [];

    public bool DraftMoveResult { get; set; } = true;

    public List<MoveDiffCursorEffect> DiffCursorMoves { get; } = [];

    public int Flushed { get; private set; }

    public bool FlushSucceeds { get; set; } = true;

    public int Quits { get; private set; }

    public List<int> CursorMoves { get; } = [];

    public List<string> OpenedFiles { get; } = [];

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

    public void MoveDiffCursor(MoveDiffCursorEffect effect) => DiffCursorMoves.Add(effect);

    public void LaunchExternalTool(LaunchExternalToolEffect effect) => Tools.Add(effect);

    public bool MoveCommitDraft(TabId missing, TabId target)
    {
        DraftMoves.Add(new MoveCommitDraftEffect(missing, target));

        return DraftMoveResult;
    }

    public bool FlushSession()
    {
        Flushed++;

        return FlushSucceeds;
    }

    public void MoveDetailCursor(int row) => CursorMoves.Add(row);

    public void OpenFileContent(FileTreeRow row) => OpenedFiles.Add(row.Path);

    public void Quit() => Quits++;
}
