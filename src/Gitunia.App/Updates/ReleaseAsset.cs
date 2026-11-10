namespace Gitunia.App.Updates;

/// <summary>One file attached to a release.</summary>
/// <param name="Name">The file name, such as <c>gitunia-1.2.3-linux-x64.tar.gz</c>.</param>
/// <param name="Url">Where it is downloaded from.</param>
public sealed record ReleaseAsset(string Name, Uri Url);
