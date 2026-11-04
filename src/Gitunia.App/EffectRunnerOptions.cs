using Gitunia.App.Picker;
using Gitunia.App.Settings;
using Gitunia.App.Status;
using Gitunia.App.Tabs;
using Gitunia.App.Tabs.Models;
using Gitunia.Core.Keymap;
using Gitunia.Git.Watching.Models;

namespace Gitunia.App;

/// <summary>
/// The optional collaborators and tuning values of an <see cref="EffectRunner{TSession}"/>. Every property has a default,
/// so a caller (a test above all) sets only what it replaces.
/// </summary>
/// <typeparam name="TSession">The session type of the runner.</typeparam>
public sealed record EffectRunnerOptions<TSession>
    where TSession : TabSession
{
    /// <summary>Where status messages go; a reporter on the runner's dispatcher when null.</summary>
    public StatusReporter? Statuses { get; init; }

    /// <summary>File watcher tuning of each tab.</summary>
    public RepositoryWatcherOptions? Watcher { get; init; }

    /// <summary>How many tabs keep their loaded graph.</summary>
    public int WarmTabLimit { get; init; } = EffectRunner<TSession>.DefaultWarmTabLimit;

    /// <summary>Poll intervals of each tab.</summary>
    public PollOptions? Poll { get; init; }

    /// <summary>What only the terminal layer can do; nothing when null.</summary>
    public ShellRoles? ShellParts { get; init; }

    /// <summary>One object that performs every shell effect (tests); <see cref="ShellParts"/> wins when both are set.</summary>
    public IShellEffects? Shell { get; init; }

    /// <summary>The settings screen services; the settings effects are not handled when null.</summary>
    public SettingsServices? Settings { get; init; }

    /// <summary>The file system and timings of the repository picker.</summary>
    public PickerServices? Picker { get; init; }

    /// <summary>The running key bindings: the keys a message names are read from it (#1196); none when null.</summary>
    public KeyBindingTable? Keymap { get; init; }

    /// <summary>The registry the sessions are kept in, when the caller reads it too.</summary>
    public TabSessionRegistry<TSession>? Sessions { get; init; }
}
