using System.Runtime.InteropServices;
using Gitunia.Platform.Updates;

namespace Gitunia.App.Updates;

/// <summary>What one <c>gitunia update</c> run works with.</summary>
/// <param name="CurrentVersion">The version of the running binary (the build's version text).</param>
/// <param name="Facts">Where the binary lives and how it was installed, null when its path is unknown.</param>
/// <param name="Architecture">The processor architecture, to pick the archive.</param>
/// <param name="CheckOnly">Only report whether an update exists (<c>--check</c>); change nothing.</param>
public sealed record UpdateRequest(string CurrentVersion, InstallFacts? Facts, Architecture Architecture, bool CheckOnly);
