using Gitunia.Core.Config.Models;
using Gitunia.Core.Settings;
using Gitunia.Core.Settings.Models;

namespace Gitunia.App.Settings;

/// <summary>
/// Reads and writes the config file and looks folders up off the UI thread (a scan root on a slow mount must not freeze the UI) and posts
/// exactly one result command for each effect. It never reads or changes state; the reducer never lets two run at once.
/// </summary>
/// <param name="services">The config file, the machine facts, the settings on offer and the key bindings.</param>
/// <param name="dispatcher">Where the result commands go.</param>
public sealed class SettingsEffectHandler(SettingsServices services, IDispatcher dispatcher)
{
    /// <summary>Starts the effect and returns at once; the result arrives through the dispatcher.</summary>
    /// <param name="effect">What to do.</param>
    /// <param name="cancellationToken">Ends the file work when the app is closing.</param>
    public void Handle(SettingsEffect effect, CancellationToken cancellationToken)
    {
        switch (effect)
        {
            case ReadSettingsEffect read:
                Start(() => ReadAsync(read, cancellationToken));
                break;
            case WriteSettingEffect write:
                Start(() => WriteAsync(write, cancellationToken));
                break;
            case CheckSettingsFolderEffect check:
                Start(() => CheckFolderAsync(check));
                break;
            default:
                throw new UnhandledEffectException(effect);
        }
    }

    private static string ReadFailure(Exception exception) => $"Could not read config.json: {exception.Message}";

    private static string WriteFailure(Exception exception) => $"Could not save the setting: {exception.Message}";

    private void Start(Func<Task<SettingsCommand>> work) => _ = Task.Run(() => RunAsync(work), CancellationToken.None);

    private async Task RunAsync(Func<Task<SettingsCommand>> work)
    {
        SettingsCommand result;
        try
        {
            result = await work().ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            result = new SettingsOperationCancelled();
        }

        dispatcher.Post(result);
    }

    private async Task<SettingsCommand> ReadAsync(ReadSettingsEffect effect, CancellationToken token)
    {
        try
        {
            var snapshot = await services.Writer.ReadAsync(token).ConfigureAwait(false);
            var read = new SettingsRead(snapshot, Parse(snapshot));
            var opens = effect.Intent is SettingsReadIntent.Open or SettingsReadIntent.OpenAtTheme;
            var reference = opens ? KeyReferenceBuilder.Build(services.Keymap) : [];

            return new SettingsFileRead(effect.Intent, read, services.Definitions, reference);
        }
        catch (Exception exception) when (exception is not (OperationCanceledException or OutOfMemoryException))
        {
            return new SettingsOperationFailed(ReadFailure(exception));
        }
    }

    private Task<SettingsCommand> CheckFolderAsync(CheckSettingsFolderEffect effect)
    {
        SettingsCommand result;
        try
        {
            result = new SettingsFolderChecked(effect.Check, services.Environment.DirectoryExists(effect.Check.Path));
        }
        catch (Exception exception) when (exception is not (OperationCanceledException or OutOfMemoryException))
        {
            result = new SettingsOperationFailed($"Could not check the folder: {exception.Message}");
        }

        return Task.FromResult(result);
    }

    private async Task<SettingsCommand> WriteAsync(WriteSettingEffect effect, CancellationToken token)
    {
        try
        {
            var commit = effect.Commit;
            var result = await services.Writer
                .SetAsync(commit.Definition.ConfigPath, effect.Json, effect.Snapshot, token)
                .ConfigureAwait(false);
            var changed = result.Status is ConfigWriteStatus.Written or ConfigWriteStatus.Stale;
            var read = changed ? new SettingsRead(result.Snapshot, Parse(result.Snapshot)) : null;

            return new SettingsWritten(commit, result, read);
        }
        catch (Exception exception) when (exception is not (OperationCanceledException or OutOfMemoryException))
        {
            return new SettingsOperationFailed(WriteFailure(exception));
        }
    }

    private SettingsLoad Parse(ConfigSnapshot snapshot) =>
        SettingsValueReader.Read(
            snapshot,
            services.Environment.HomeDirectory,
            services.Environment.DirectoryExists,
            services.Environment.ThemeNames,
            services.Environment.DefaultLabelCaps);
}
