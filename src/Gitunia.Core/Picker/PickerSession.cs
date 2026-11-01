using System.Collections.Immutable;

namespace Gitunia.Core.Picker;

/// <summary>
/// The repository picker as the shell state holds it: the open picker (null while closed) and what the background work
/// needs between commands. Changed only by <see cref="PickerSessionReducer"/>.
/// </summary>
/// <param name="State">The open picker, null while it is closed.</param>
/// <param name="Discovered">Repository root paths the scanner found so far.</param>
/// <param name="ScanRoots">The folders the next scan looks in, null for the conventional ones.</param>
/// <param name="HomeDirectory">What <c>~</c> expands to (set when the app starts).</param>
/// <param name="LastScan">When the last scan started, null when none ran or the roots changed since.</param>
/// <param name="IsScanning">A scan runs.</param>
/// <param name="CompleteAfterListing">Tab was pressed during a listing: the path is completed once the candidates
/// arrive.</param>
/// <param name="OpenRequest">Counts the opens of a path, so a result that arrives after a close or a newer open is
/// stale.</param>
public sealed record PickerSession(
    PickerState? State,
    ImmutableArray<string> Discovered,
    ImmutableArray<string>? ScanRoots,
    string HomeDirectory,
    DateTimeOffset? LastScan,
    bool IsScanning,
    bool CompleteAfterListing,
    int OpenRequest)
{
    public static PickerSession Empty { get; } = new(null, [], null, string.Empty, null, false, false, 0);

    public bool IsOpen => State is not null;
}
