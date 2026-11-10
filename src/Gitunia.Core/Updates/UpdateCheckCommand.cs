using Gitunia.Core.Shell;

namespace Gitunia.Core.Updates;

/// <summary>A command of the in-app update hint (#1292).</summary>
public abstract record UpdateCheckCommand : ShellCommand;

/// <summary>
/// The terminal layer asks for the check, after the first frame and again when the setting changes. The command carries
/// the setting (with the <c>GITUNIA_NO_UPDATE_CHECK</c> variable already applied) because the state does not hold the options.
/// </summary>
/// <param name="CurrentVersion">The version of the running build.</param>
/// <param name="Enabled">Whether the user allows the check.</param>
public sealed record UpdateCheckRequested(string CurrentVersion, bool Enabled) : UpdateCheckCommand;

/// <summary>The check ended.</summary>
/// <param name="Notice">The newer release, or null when the build is current or the check found nothing (an error shows nothing).</param>
public sealed record UpdateCheckCompleted(UpdateNotice? Notice) : UpdateCheckCommand;
