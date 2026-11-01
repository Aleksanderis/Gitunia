using Gitunia.Core.Config.Models;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Settings.Models;

namespace Gitunia.Core.Settings;

/// <summary>A command on the settings overlay: user intent and the results of its effects.</summary>
public abstract record SettingsCommand : AppCommand;

/// <summary>A bound action: opens the screen, toggles the command level while it is closed, and while it is open
/// consumes every action.</summary>
/// <param name="Action">The action.</param>
/// <param name="Count">The numeric prefix, null when none was typed.</param>
public sealed record SettingsActionPressed(ActionId Action, int? Count) : SettingsCommand;

/// <summary>A key typed into the number box, the folder box or the filter.</summary>
/// <param name="Key">The key.</param>
public sealed record SettingsKeyTyped(KeyEvent Key) : SettingsCommand;

/// <summary>The terminal size changed: the lists have another page size and the layout may collapse or
/// expand.</summary>
/// <param name="Viewport">The new room.</param>
public sealed record SettingsViewportChanged(SettingsViewport Viewport) : SettingsCommand;

/// <summary>A click on a category.</summary>
/// <param name="Index">The category.</param>
public sealed record SettingsCategoryClicked(int Index) : SettingsCommand;

/// <summary>A click on a settings row: the cursor goes there.</summary>
/// <param name="Index">The row.</param>
public sealed record SettingsRowClicked(int Index) : SettingsCommand;

/// <summary>The pointer is over a column (the wheel scrolls it): the cursor goes there.</summary>
/// <param name="Column">The column under the pointer.</param>
public sealed record SettingsColumnFocused(SettingsColumn Column) : SettingsCommand;

/// <summary>The editor the config file was opened in has closed: read the file again.</summary>
public sealed record SettingsEditorClosed : SettingsCommand;

/// <summary>The config file was read.</summary>
/// <param name="Intent">Why it was read.</param>
/// <param name="Read">The file and the settings parsed from it.</param>
/// <param name="Definitions">The settings on offer.</param>
/// <param name="KeyReference">The key reference, built for an open (empty otherwise).</param>
public sealed record SettingsFileRead(
    SettingsReadIntent Intent,
    SettingsRead Read,
    IReadOnlyList<SettingDefinition> Definitions,
    IReadOnlyList<KeyReferenceEntry> KeyReference) : SettingsCommand;

/// <summary>A change was written (or refused, or found stale).</summary>
/// <param name="Commit">What was asked.</param>
/// <param name="Result">How the write ended.</param>
/// <param name="Read">The file after the write, null when nothing changed.</param>
public sealed record SettingsWritten(SettingsCommit Commit, ConfigWriteResult Result, SettingsRead? Read)
    : SettingsCommand;

/// <summary>Reading or writing the file failed.</summary>
/// <param name="Text">What to tell the user.</param>
public sealed record SettingsOperationFailed(string Text) : SettingsCommand;

/// <summary>Reading or writing the file was cancelled (the app is closing).</summary>
public sealed record SettingsOperationCancelled : SettingsCommand;
