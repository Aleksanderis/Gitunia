using System.Text.Json.Serialization;

namespace Gitunia.App.Updates;

/// <summary>Source-generated JSON metadata for the release document and the update check cache (Native AOT: no reflection).</summary>
[JsonSerializable(typeof(GitHubReleaseDocument))]
[JsonSerializable(typeof(UpdateCheckRecord))]
public sealed partial class UpdateJsonContext : JsonSerializerContext;
