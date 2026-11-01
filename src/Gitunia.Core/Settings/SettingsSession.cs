using Gitunia.Core.Config.Models;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Settings.Models;

namespace Gitunia.Core.Settings;

/// <summary>
/// The settings overlay as the shell state holds it: the open screen (null while closed) and what the file work and the
/// theme preview need between commands. Changed only by <see cref="SettingsScreenReducer"/>.
/// </summary>
/// <param name="Screen">The open screen, null while it is closed.</param>
/// <param name="HomeDirectory">What a leading <c>~</c> in a folder stands for (set when the app starts).</param>
/// <param name="Viewport">What the terminal layer reported about the room the screen has.</param>
/// <param name="Snapshot">The file as last read, for the stale check of the next write.</param>
/// <param name="IsBusy">True while the file is read or written; reads and writes never overlap.</param>
/// <param name="Mode">The key mode the screen asked for last (Insert while typing, Confirm for the removal
/// question).</param>
/// <param name="Committed">The theme of the file the running app uses.</param>
/// <param name="Pending">The theme just kept, until its write is answered.</param>
/// <param name="Shown">The theme on screen.</param>
public sealed record SettingsSession(
    SettingsState? Screen,
    string HomeDirectory,
    SettingsViewport Viewport,
    ConfigSnapshot Snapshot,
    bool IsBusy,
    KeyMode Mode,
    ThemeChoice? Committed,
    ThemeChoice? Pending,
    ThemeChoice? Shown)
{
    public static SettingsSession Empty { get; } = new(
        null,
        string.Empty,
        new SettingsViewport(1, false),
        new ConfigSnapshot(false, string.Empty, string.Empty),
        false,
        KeyMode.Normal,
        null,
        null,
        null);

    public bool IsOpen => Screen is not null;

    /// <summary>True when typed keys go to the number box, the folder box or the filter.</summary>
    public bool TakesText => Screen is { IsTyping: true };
}
