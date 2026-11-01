using System.Collections.Immutable;

namespace Gitunia.Core.Settings;

/// <summary>The result of a settings command.</summary>
/// <param name="Session">The new session.</param>
/// <param name="Effects">Effects to run.</param>
public sealed record SettingsUpdate(SettingsSession Session, ImmutableArray<AppEffect> Effects);
