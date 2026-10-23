namespace Gitunia.Platform.Urls;

/// <summary>Opens an address in the user's default browser.</summary>
public interface IUrlOpener
{
    /// <summary>False on an OS with no opener, where <see cref="Open"/> always fails.</summary>
    bool IsSupported { get; }

    /// <summary>
    /// Starts the system opener for an http or https address without waiting for it. Never throws: an address of
    /// another scheme, a missing opener program or an unsupported OS give a failed result.
    /// </summary>
    UrlOpenResult Open(string url);
}
