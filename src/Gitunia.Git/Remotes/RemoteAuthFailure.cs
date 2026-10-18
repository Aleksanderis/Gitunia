namespace Gitunia.Git.Remotes;

/// <summary>
/// Tells whether a failed remote call failed because it needed credentials, a passphrase or a host key confirmation,
/// which the background run (no prompts, ssh in batch mode) cannot give. Matched on git's and ssh's stderr, which is
/// in the C locale for our calls. A missing repository or a refused push is not an authentication failure.
/// </summary>
public static class RemoteAuthFailure
{
    private static readonly string[] Markers =
    [
        "Permission denied (publickey",
        "Permission denied (password",
        "Permission denied (keyboard-interactive",
        "could not read Username",
        "could not read Password",
        "terminal prompts disabled",
        "Authentication failed",
        "Host key verification failed",
        "error: The requested URL returned error: 401",
    ];

    public static bool Matches(string standardError) =>
        Markers.Any(marker => standardError.Contains(marker, StringComparison.Ordinal));
}
