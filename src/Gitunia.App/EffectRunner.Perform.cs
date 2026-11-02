using Gitunia.Core.Running;
using Gitunia.Core.Worktrees;
using Gitunia.Core.Status;
using Gitunia.Core.Shell;
using Gitunia.Core.Blame;
using Gitunia.Core.Reflog;
using Gitunia.Core.Picker;
using Gitunia.Core.Creation;
using Gitunia.Core.Commits;
using Gitunia.Core.Workspaces;
using Gitunia.Core.Settings;
using Gitunia.Core.Effects;
using Gitunia.Core;

namespace Gitunia.App;

// Failure commands and the settings, confirm and cancel effects.
public sealed partial class EffectRunner<TSession>
{
    // An operation effect names its own failure (OperationEffect.FailedWith), so its running mark always ends. Only
    // effects that are not operations are mapped here, to the result command that ends their own busy flag.
    private static AppCommand FailureOf(AppEffect effect, string text) => effect switch
    {
        OperationEffect operation => operation.FailedWith(text),
        CloneRepositoryEffect => new CloneFailed(text),
        DiscoverRepositoryEffect discover => new PickerDiscoveryFailed(discover.Request, text),
        InitRepositoryEffect => new InitFailed(text),
        ReadHeadMessageEffect head => new HeadMessageReadFailed(head.TabId, text),
        ReadReflogEffect => new ReflogReadFailed(text),
        StartBlameEffect blame => new BlameFailed(blame.Generation, text),
        ScanRepositoriesEffect => new PickerScanFinished([]),
        SettingsEffect => new SettingsOperationFailed(text),
        LocateWorktreeEffect locate => new WorktreeLocateFailed(locate.TabId, text),
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
                _shell.Session.ApplyOptions(options.Options);
                break;
            case ApplySettingsThemeEffect theme:
                _shell.Session.ApplyTheme(theme.Theme);
                break;
            case OpenConfigEditorEffect:
                _shell.Tools.OpenConfigEditor();
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
}
