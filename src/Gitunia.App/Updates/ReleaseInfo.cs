using Gitunia.Core.Updates;

namespace Gitunia.App.Updates;

/// <summary>A published release: its version and the files attached to it.</summary>
/// <param name="Version">The version from the release tag.</param>
/// <param name="Assets">The attached files (archives, packages and their <c>.sha256</c> files).</param>
public sealed record ReleaseInfo(ReleaseVersion Version, IReadOnlyList<ReleaseAsset> Assets)
{
    /// <summary>The first asset whose name ends with <paramref name="suffix"/>, null when there is none.</summary>
    public ReleaseAsset? FindBySuffix(string suffix)
    {
        foreach (var asset in Assets)
        {
            if (asset.Name.EndsWith(suffix, StringComparison.Ordinal))
            {
                return asset;
            }
        }

        return null;
    }

    /// <summary>The asset with exactly this name, null when there is none.</summary>
    public ReleaseAsset? FindByName(string name)
    {
        foreach (var asset in Assets)
        {
            if (asset.Name == name)
            {
                return asset;
            }
        }

        return null;
    }
}
