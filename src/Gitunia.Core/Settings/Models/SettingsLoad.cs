using System.Collections.Immutable;
using Gitunia.Core.Config.Models;

namespace Gitunia.Core.Settings.Models;

/// <summary>The settings as read from the config file.</summary>
/// <param name="Values">The value of every setting by id.</param>
/// <param name="Warnings">The reason a value in the file is not valid, by setting id.</param>
/// <param name="Banner">Why the file cannot be edited, empty when it can.</param>
/// <param name="Options">The options the running app should use.</param>
/// <param name="Theme">The theme the running app should use.</param>
public sealed record SettingsLoad(
    ImmutableDictionary<string, SettingValue> Values,
    ImmutableDictionary<string, string> Warnings,
    string Banner,
    ConfigOptions Options,
    ThemeChoice Theme);
