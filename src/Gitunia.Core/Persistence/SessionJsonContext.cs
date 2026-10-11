using System.Text.Json;
using System.Text.Json.Serialization;

namespace Gitunia.Core.Persistence;

/// <summary>Source-generated JSON metadata for the session file (Native AOT: no reflection).</summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    ReadCommentHandling = JsonCommentHandling.Skip,
    AllowTrailingCommas = true)]
[JsonSerializable(typeof(SessionDocument))]
public sealed partial class SessionJsonContext : JsonSerializerContext;
