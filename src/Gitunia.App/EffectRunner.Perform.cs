using Gitunia.Core.Worktrees;
using Gitunia.Core.Tags;
using Gitunia.Core.Sync;
using Gitunia.Core.Status;
using Gitunia.Core.Stash;
using Gitunia.Core.Staging;
using Gitunia.Core.Shell;
using Gitunia.Core.Remotes;
using Gitunia.Core.Reflog;
using Gitunia.Core.Rebase;
using Gitunia.Core.Picker;
using Gitunia.Core.Pick;
using Gitunia.Core.Integration;
using Gitunia.Core.Discard;
using Gitunia.Core.Creation;
using Gitunia.Core.Conflicts;
using Gitunia.Core.Commits;
using Gitunia.Core.Branches;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Core.Workspaces;
using Gitunia.Core.Settings;
using Gitunia.Core.Effects;
using Gitunia.Core;

namespace Gitunia.App;

// Failure commands and the settings, confirm and cancel effects.
public sealed partial class EffectRunner<TSession>
{
    // A feature's effect fails with that feature's result command, so its running flag is cleared.
    private static AppCommand FailureOf(AppEffect effect, string text) => effect switch
    {
        CloneRepositoryEffect => new CloneFailed(text),
        DiscoverRepositoryEffect discover => new PickerDiscoveryFailed(discover.Request, text),
        InitRepositoryEffect => new InitFailed(text),
        IntegrationEffect integration => new IntegrationFailed(integration.TabId, text),
        BranchEffect branch => new BranchOperationFailed(branch.TabId, text),
        ReadHeadMessageEffect head => new HeadMessageReadFailed(head.TabId, text),
        DiscardEffect discard => new DiscardFailed(discard.TabId, text),
        RunCommitEffect commit => new CommitFailed(commit.TabId, text, null),
        ConflictEffect conflict => new ConflictOperationFailed(conflict.TabId, text, StatusSeverity.Error),
        PickEffect pick => new PickFailed(pick.TabId, text),
        RebaseEffect rebase => new RebaseFailed(rebase.TabId, text),
        RunInteractiveRebaseEffect run => new RebaseFailed(run.TabId, text),
        RebaseEditorEffect editor => new RebaseEditorReadFailed(editor.TabId, text, StatusSeverity.Error),
        ReadReflogEffect => new ReflogReadFailed(text),
        ScanRepositoriesEffect => new PickerScanFinished([]),
        SettingsEffect => new SettingsOperationFailed(text),
        RemoteEffect remote => new RemoteOperationFailed(remote.TabId, text),
        StagingEffect staging => new StagingFailed(staging.TabId, text),
        StashEffect stash => new StashOperationFailed(stash.TabId, text),
        SyncEffect sync => new SyncFailed(sync.TabId, text, null, null),
        TagEffect tag => new TagOperationFailed(tag.TabId, text, null),
        LocateWorktreeEffect locate => new WorktreeLocateFailed(locate.TabId, text),
        WorktreeEffect worktree => new WorktreeOperationFailed(worktree.TabId, text),
        _ => new EffectFailedCommand(text),
    };

    // Without settings services (a runner without a config file) the file work fails at once, so nothing stays busy.
    private void PerformSettings(SettingsEffect effect)
    {
        if (_settings is null)
        {
            _host.Dispatch(new SettingsOperationFailed("The config file is not available"));

            return;
        }

        _settings.Handle(effect, _shutdown.Token);
    }

    private void PerformSettingsShell(SettingsShellEffect effect)
    {
        switch (effect)
        {
            case ApplySettingsOptionsEffect options:
                _shell.ApplyOptions(options.Options);
                break;
            case ApplySettingsThemeEffect theme:
                _shell.ApplyTheme(theme.Theme);
                break;
            case OpenConfigEditorEffect:
                _shell.OpenConfigEditor();
                break;
            default:
                throw new UnhandledEffectException(effect);
        }
    }

    private void ShowConfirm(ShowConfirmEffect confirm)
    {
        var refusal = new StatusPostedCommand(confirm.UnavailableNotice, StatusSeverity.Warning, _host.Clock.GetUtcNow());
        _host.Dispatch(new ConfirmRequestedCommand(confirm.Prompt, confirm.OnAccepted, refusal));
    }

    private void Cancel(CancelLoadEffect cancel)
    {
        _cancellation.Cancel(cancel.TabId);
        _pendingLoads.Remove(cancel.TabId);
        _recent.Remove(cancel.TabId);
        DisposeRefresher(cancel.TabId);
        if (_sessions.Remove(cancel.TabId, out var closed))
        {
            closed.Dispose();
        }
    }

    // A tab runs an operation when its state says so; such a tab is never evicted.
    private bool IsBusy(TabId tabId) => _host.Workspace.FindTab(tabId)?.Running.IsBusy == true;
}
