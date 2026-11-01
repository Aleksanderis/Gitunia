namespace Gitunia.App.Tests.Fakes;

/// <summary>Paths under one root for picker tests, no disk access.</summary>
public static class PickerFiles
{
    // Under the drive root: a Windows temp path ("...\AppData\Local\Temp") would match short queries.
    public static string Home { get; } = Path.Combine(Path.GetPathRoot(Path.GetTempPath())!, "picker-tests", "home");

    public static string BaseDirectory { get; } = PathOf("code", "current");

    public static string PathOf(params string[] segments) => Path.Combine([Home, .. segments]);
}
