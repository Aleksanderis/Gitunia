namespace Gitunia.App.Picker;

/// <summary>How long the listing of a typed path may take before it shows a notice or gives up.</summary>
/// <param name="ListingTimeout">The listing gives up after this long (a slow file system).</param>
/// <param name="ScanningDelay">The scanning notice shows after this long.</param>
public sealed record PickerTimings(TimeSpan ListingTimeout, TimeSpan ScanningDelay)
{
    public static PickerTimings Default { get; } = new(TimeSpan.FromSeconds(5), TimeSpan.FromMilliseconds(150));
}
