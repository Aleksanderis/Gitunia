using Gitunia.Core.Settings;

namespace Gitunia.App;

// The settings effects of the runner: file work in the handler, terminal-side changes through the shell.
public sealed partial class EffectRunner<TSession>
{
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
}
