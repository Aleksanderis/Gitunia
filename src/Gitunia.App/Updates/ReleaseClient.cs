using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Gitunia.Core.Updates;

namespace Gitunia.App.Updates;

/// <summary>
/// Reads the latest release and downloads its files over HTTP. The only data a request carries is the
/// <c>User-Agent</c> (<c>gitunia/&lt;version&gt;</c>): no token, account or machine id. The client comes from the caller
/// (<see cref="Create"/> in production, a fake handler in tests), so proxy settings and timeouts are decided in one place.
/// </summary>
public sealed class ReleaseClient(HttpClient client, Uri latestReleaseUrl)
{
    public const string DefaultLatestReleaseUrl = "https://api.github.com/repos/Aleksanderis/Gitunia/releases/latest";

    private const string UserAgentProduct = "gitunia";
    private const string GitHubMediaType = "application/vnd.github+json";
    private const int DownloadBufferSize = 81920;
    private static readonly TimeSpan MetadataTimeout = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan DownloadTimeout = TimeSpan.FromMinutes(5);

    /// <summary>
    /// An <see cref="HttpClient"/> that follows the system proxy (with the user's default credentials, for corporate proxies)
    /// and has no overall timeout: <see cref="ReleaseClient"/> bounds every call itself.
    /// </summary>
    public static HttpClient Create(string version)
    {
        var handler = new SocketsHttpHandler
        {
            UseProxy = true,
            DefaultProxyCredentials = CredentialCache.DefaultCredentials,
            ConnectTimeout = MetadataTimeout,
            AllowAutoRedirect = true,
        };
        var created = new HttpClient(handler, disposeHandler: true) { Timeout = Timeout.InfiniteTimeSpan };
        created.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue(UserAgentProduct, version));

        return created;
    }

    /// <summary>The latest release; an <see cref="UpdateException"/> says why it could not be read.</summary>
    public async Task<ReleaseInfo> GetLatestAsync(CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(MetadataTimeout);
        using var request = new HttpRequestMessage(HttpMethod.Get, latestReleaseUrl);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(GitHubMediaType));

        GitHubReleaseDocument? document;
        try
        {
            using var response = await client.SendAsync(request, timeout.Token);
            response.EnsureSuccessStatusCode();
            await using var stream = await response.Content.ReadAsStreamAsync(timeout.Token);
            document = await JsonSerializer.DeserializeAsync(
                stream,
                UpdateJsonContext.Default.GitHubReleaseDocument,
                timeout.Token);
        }
        catch (Exception exception) when (IsFailure(exception, cancellationToken))
        {
            throw new UpdateException($"could not read the latest release from {latestReleaseUrl} ({Describe(exception)})", exception);
        }

        return ToRelease(document);
    }

    /// <summary>Downloads <paramref name="url"/> to <paramref name="destination"/>, replacing a file that is there.</summary>
    public async Task DownloadFileAsync(Uri url, string destination, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(DownloadTimeout);
        try
        {
            using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            response.EnsureSuccessStatusCode();
            await using var source = await response.Content.ReadAsStreamAsync(timeout.Token);
            await using var target = new FileStream(
                destination,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                DownloadBufferSize,
                useAsync: true);
            await source.CopyToAsync(target, DownloadBufferSize, timeout.Token);
            await VerifyComplete(response, target, timeout.Token);
        }
        catch (Exception exception) when (IsFailure(exception, cancellationToken))
        {
            throw new UpdateException($"download failed: {url} ({Describe(exception)})", exception);
        }
    }

    /// <summary>Downloads a small text file.</summary>
    public async Task<string> DownloadTextAsync(Uri url, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(MetadataTimeout);
        try
        {
            using var response = await client.GetAsync(url, timeout.Token);
            response.EnsureSuccessStatusCode();

            return await response.Content.ReadAsStringAsync(timeout.Token);
        }
        catch (Exception exception) when (IsFailure(exception, cancellationToken))
        {
            throw new UpdateException($"download failed: {url} ({Describe(exception)})", exception);
        }
    }

    private static ReleaseInfo ToRelease(GitHubReleaseDocument? document)
    {
        if (document?.TagName is null || !ReleaseVersion.TryParse(document.TagName, out var version))
        {
            throw new UpdateException($"the latest release has no usable version tag ({document?.TagName ?? "none"})");
        }

        var assets = new List<ReleaseAsset>();
        foreach (var asset in document.Assets ?? [])
        {
            if (asset.Name is not null && Uri.TryCreate(asset.BrowserDownloadUrl, UriKind.Absolute, out var url))
            {
                assets.Add(new ReleaseAsset(asset.Name, url));
            }
        }

        return new ReleaseInfo(version, assets);
    }

    // A server that closed the connection early gives a short body without an error; the length it announced catches it.
    private static async Task VerifyComplete(HttpResponseMessage response, FileStream target, CancellationToken cancellationToken)
    {
        await target.FlushAsync(cancellationToken);
        var announced = response.Content.Headers.ContentLength;
        if (announced is { } length && length != target.Length)
        {
            throw new IOException($"received {target.Length} of {length} bytes");
        }
    }

    // Everything the network and the disk throw, except the caller's own cancellation.
    private static bool IsFailure(Exception exception, CancellationToken cancellationToken)
    {
        if (exception is OperationCanceledException)
        {
            return !cancellationToken.IsCancellationRequested;
        }

        return exception is HttpRequestException or IOException or JsonException or UnauthorizedAccessException;
    }

    private static string Describe(Exception exception)
    {
        if (exception is OperationCanceledException)
        {
            return "timed out";
        }

        return exception.Message;
    }
}
