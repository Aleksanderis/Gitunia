namespace Gitunia.App.Picker;

/// <summary>What the picker and creation handlers read from the machine; the real file system and delays in the app.</summary>
/// <param name="Files">The file system questions.</param>
/// <param name="Timings">The listing delays.</param>
public sealed record PickerServices(IDirectoryLister Files, PickerTimings Timings)
{
    public static PickerServices Default { get; } = new(DiskDirectories.Instance, PickerTimings.Default);
}
