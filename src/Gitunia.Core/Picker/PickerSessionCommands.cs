using System.Collections.Immutable;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Repositories.Models;

namespace Gitunia.Core.Picker;

/// <summary>Open the picker, or reset it when it is open.</summary>
/// <param name="LocatingTabId">The missing tab the chosen repository replaces, null for a normal open.</param>
/// <param name="InitialQuery">Text to start with.</param>
/// <param name="Now">The time, for the rescan interval.</param>
/// <param name="KeepIfOpen">An open picker stays as it is (a second click on "+" must not drop the query).</param>
/// <param name="OnlyWithoutTabs">Opens only when no tab is open (after the last tab closed).</param>
public sealed record PickerOpenRequested(
    TabId? LocatingTabId,
    string InitialQuery,
    DateTimeOffset Now,
    bool KeepIfOpen = false,
    bool OnlyWithoutTabs = false) : PickerCommand;

/// <summary>Close the picker.</summary>
public sealed record PickerCloseRequested : PickerCommand;

/// <summary>Esc in the picker: closes it, or quits when no tab is open.</summary>
/// <param name="AnyUnsentMessage">Whether any tab holds an unsent commit message.</param>
public sealed record PickerCancelPressed(bool AnyUnsentMessage) : PickerCommand;

/// <summary>Open the highlighted repository or the typed path; a repository that is already open just gets focus.</summary>
public sealed record PickerAcceptPressed : PickerCommand;

/// <summary>Pin or unpin the highlighted repository.</summary>
public sealed record PickerPinPressed : PickerCommand;

/// <summary>Forget the highlighted repository in the recents list. Nothing on disk is touched.</summary>
public sealed record PickerRemoveRecentPressed : PickerCommand;

/// <summary>The folders to scan changed (the settings screen).</summary>
/// <param name="Roots">The folders, null for the conventional ones.</param>
public sealed record PickerScanRootsChanged(ImmutableArray<string>? Roots) : PickerCommand;

/// <summary>Result: the scan of the repositories finished.</summary>
/// <param name="Found">The repository root paths.</param>
public sealed record PickerScanFinished(ImmutableArray<string> Found) : PickerCommand;

/// <summary>Result: a directory was resolved to its repository.</summary>
/// <param name="Request">The open request it answers.</param>
/// <param name="Location">The repository.</param>
/// <param name="Replacing">The missing tab it replaces, null for a normal open.</param>
public sealed record PickerRepositoryFound(int Request, RepositoryLocation Location, TabId? Replacing)
    : PickerCommand;

/// <summary>Result: the directory is no repository, or reading it failed.</summary>
/// <param name="Request">The open request it answers.</param>
/// <param name="Message">What to tell the user.</param>
public sealed record PickerDiscoveryFailed(int Request, string Message) : PickerCommand;

/// <summary>The repository is in a tab now: finish the open (move the draft of a replaced tab, close the picker).</summary>
/// <param name="Replacing">The missing tab it replaces, null for a normal open.</param>
/// <param name="Location">The repository.</param>
public sealed record PickerRepositoryOpened(TabId? Replacing, RepositoryLocation Location) : PickerCommand;

/// <summary>Result: the commit draft of the missing tab was moved to the replacing tab, or both hold a message.</summary>
/// <param name="Missing">The missing tab.</param>
/// <param name="Moved">False when the other tab holds its own message.</param>
public sealed record PickerDraftMoved(TabId Missing, bool Moved) : PickerCommand;

/// <summary>A repository the app just created (clone, init) opens in a new tab; the picker closes.</summary>
/// <param name="Location">The repository.</param>
public sealed record PickerRepositoryCreated(RepositoryLocation Location) : PickerCommand;
