using System.Text.Json.Serialization;

namespace Gitunia.App.Updates;

/// <summary>The fields of GitHub's "latest release" JSON that the update uses.</summary>
public sealed record GitHubReleaseDocument(
    [property: JsonPropertyName("tag_name")] string? TagName,
    [property: JsonPropertyName("assets")] List<GitHubAssetDocument>? Assets);

/// <summary>One entry of the release's <c>assets</c> list.</summary>
public sealed record GitHubAssetDocument(
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("browser_download_url")] string? BrowserDownloadUrl);
