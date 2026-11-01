using System.Collections.Immutable;
using Gitunia.Core.Workspaces.Tabs;

namespace Gitunia.Core.Picker;

/// <summary>Work for the repository picker, run off the UI thread. Each posts its result command.</summary>
public abstract record PickerEffect : AppEffect
{
    private protected PickerEffect()
    {
    }
}

/// <summary>List the directories that continue a typed path (replaces a listing that runs). Posts
/// <see cref="ShowScanningCommand"/> when it is slow and <see cref="ListingCompletedCommand"/> at the end.</summary>
/// <param name="Query">The typed path.</param>
/// <param name="HomeDirectory">What <c>~</c> expands to.</param>
/// <param name="BaseDirectory">What a relative path is resolved against.</param>
public sealed record ListPickerDirectoriesEffect(string Query, string HomeDirectory, string BaseDirectory)
    : PickerEffect;

/// <summary>Stop the running listing; it posts nothing.</summary>
public sealed record CancelPickerListingEffect : PickerEffect;

/// <summary>Scan the folders for repositories. Posts <see cref="PickerScanFinished"/>.</summary>
/// <param name="Roots">The folders, null for the conventional ones below the home directory.</param>
/// <param name="HomeDirectory">The home directory.</param>
public sealed record ScanRepositoriesEffect(ImmutableArray<string>? Roots, string HomeDirectory) : PickerEffect;

/// <summary>Resolve a directory to its repository. Posts <see cref="PickerRepositoryFound"/> or
/// <see cref="PickerDiscoveryFailed"/>.</summary>
/// <param name="Path">The directory.</param>
/// <param name="Request">The open request, echoed in the result.</param>
/// <param name="Replacing">The missing tab it replaces, echoed in the result.</param>
public sealed record DiscoverRepositoryEffect(string Path, int Request, TabId? Replacing) : PickerEffect;

/// <summary>Move the unsent commit message of a missing tab to the tab that replaces it (the drafts live in the terminal
/// layer). Posts <see cref="PickerDraftMoved"/>.</summary>
/// <param name="Missing">The missing tab.</param>
/// <param name="Target">The replacing tab.</param>
public sealed record MoveCommitDraftEffect(TabId Missing, TabId Target) : PickerEffect;
