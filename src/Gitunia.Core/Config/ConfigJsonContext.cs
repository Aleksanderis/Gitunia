using System.Text.Json;
using System.Text.Json.Serialization;
using Gitunia.Core.Config.Models;

namespace Gitunia.Core.Config;

/// <summary>
/// Source-generated JSON metadata for the config file (Native AOT: no reflection). Comments and
/// trailing commas are allowed.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    PropertyNameCaseInsensitive = true,
    ReadCommentHandling = JsonCommentHandling.Skip,
    AllowTrailingCommas = true)]
[JsonSerializable(typeof(ConfigDocument))]
public sealed partial class ConfigJsonContext : JsonSerializerContext;
