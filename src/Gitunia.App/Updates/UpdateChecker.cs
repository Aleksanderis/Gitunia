using Gitunia.Core.Updates;
using Gitunia.Platform.Updates;

namespace Gitunia.App.Updates;

/// <summary>
/// The in-app update hint's check (#1292): at most one request a day, the answer cached in the state directory (so the
/// hint stays on every start of the day without asking again). The request is <see cref="ReleaseClient"/>'s, so it
/// sends nothing but the <c>User-Agent</c>. A failed request shows nothing and counts as the day's attempt; the
/// previous answer stays. Writes only the cache file.
/// </summary>
/// <param name="cache">The remembered check.</param>
/// <param name="currentVersion">The version of the running build: the <c>User-Agent</c> and the one to compare with.</param>
/// <param name="latestReleaseUrl">The latest-release address.</param>
/// <param name="facts">Reads where the binary lives and how it was installed, to name the update command.</param>
/// <param name="clock">The time source.</param>
/// <param name="handler">Answers the request instead of the network (tests); null uses <see cref="ReleaseClient.Create"/>. The client is built when a request is due, not at startup.</param>
public sealed class UpdateChecker(
    UpdateCheckCache cache,
    string currentVersion,
    Uri latestReleaseUrl,
    InstallFactsReader facts,
    TimeProvider clock,
    HttpMessageHandler? handler = null)
{
    /// <summary>The shortest time between two requests.</summary>
    public static readonly TimeSpan Interval = TimeSpan.FromDays(1);

    /// <summary>The newer release to tell about, or null when the build is current, is a development build or nothing is known.</summary>
    /// <param name="cancellationToken">Ends the request.</param>
    public async Task<UpdateNotice?> CheckAsync(CancellationToken cancellationToken)
    {
        if (!ReleaseVersion.TryParse(currentVersion, out var current) || current.IsDevelopmentBuild)
        {
            return null;
        }

        var record = await cache.ReadAsync(cancellationToken);
        var now = clock.GetUtcNow();
        if (record is null || IsDue(record.CheckedAt, now))
        {
            record = await RequestAsync(record, now, cancellationToken);
        }

        if (!ReleaseVersion.TryParse(record.Latest, out var latest) || !latest.IsNewerThan(current))
        {
            return null;
        }

        return new UpdateNotice(latest.ToString(), CommandFor(facts.Read()));
    }

    // A time stamp in the future (the clock was set back) is no reason to stay silent for longer than a day.
    private static bool IsDue(DateTimeOffset checkedAt, DateTimeOffset now) =>
        now < checkedAt || now - checkedAt >= Interval;

    private static string CommandFor(InstallFacts? read) =>
        UpdateAdvice.HintCommandFor(read is null ? InstallRoute.Unknown : InstallRouteDetector.Detect(read));

    private HttpClient CreateClient()
    {
        if (handler is null)
        {
            return ReleaseClient.Create(currentVersion);
        }

        return new HttpClient(handler, disposeHandler: false);
    }

    private async Task<UpdateCheckRecord> RequestAsync(
        UpdateCheckRecord? previous,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var latest = previous?.Latest;
        try
        {
            using var http = CreateClient();
            var release = await new ReleaseClient(http, latestReleaseUrl).GetLatestAsync(cancellationToken);
            latest = release.Version.ToString();
        }
        catch (UpdateException)
        {
            // Offline, rate limited or a bad answer: no news from this request.
        }

        var record = new UpdateCheckRecord(now, latest);
        await cache.WriteAsync(record, cancellationToken);

        return record;
    }
}
