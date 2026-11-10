using System.Net;
using System.Net.Http.Headers;
using System.Text;

namespace Gitunia.App.Tests.Updates;

/// <summary>An in-memory release endpoint: answers by URL, records what was asked. No network is touched.</summary>
public sealed class FakeReleaseServer : HttpMessageHandler
{
    private readonly Dictionary<string, FakeResponse> _responses = new(StringComparer.Ordinal);
    private readonly List<HttpRequestMessage> _requests = [];

    public IReadOnlyList<HttpRequestMessage> Requests => _requests;

    public void Serve(string url, string text)
    {
        Serve(url, Encoding.UTF8.GetBytes(text));
    }

    public void Serve(string url, byte[] body, long? announcedLength = null)
    {
        _responses[url] = new FakeResponse(HttpStatusCode.OK, body, announcedLength);
    }

    public void Fail(string url, HttpStatusCode status)
    {
        _responses[url] = new FakeResponse(status, [], null);
    }

    public int CountRequestsEndingWith(string suffix) =>
        _requests.Count(request => request.RequestUri!.ToString().EndsWith(suffix, StringComparison.Ordinal));

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        _requests.Add(request);
        if (!_responses.TryGetValue(request.RequestUri!.ToString(), out var fake))
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }

        var response = new HttpResponseMessage(fake.Status) { Content = new ByteArrayContent(fake.Body) };
        response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        if (fake.AnnouncedLength is { } length)
        {
            response.Content.Headers.ContentLength = length;
        }

        return Task.FromResult(response);
    }

    private sealed record FakeResponse(HttpStatusCode Status, byte[] Body, long? AnnouncedLength);
}
