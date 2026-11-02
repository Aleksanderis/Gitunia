using Gitunia.Core.IssueLinks;
using Gitunia.Platform.Urls;

namespace Gitunia.App.IssueLinks;

/// <summary>
/// Starts the system opener for an address off the UI thread (it can stall, ShellExecute on a cold start, so the
/// dispatcher thread never waits for it) and posts exactly one <see cref="IssueLinkOpenFinished"/>.
/// </summary>
/// <param name="opener">Starts the system opener.</param>
/// <param name="dispatcher">Where the result goes.</param>
public sealed class IssueLinkEffectHandler(IUrlOpener opener, IDispatcher dispatcher)
{
    private const string OpenerFailed = "the opener failed";

    public void Handle(OpenUrlEffect effect) =>
        _ = Task.Run(() => Open(effect.Url), CancellationToken.None);

    private void Open(string url)
    {
        UrlOpenResult result;
        try
        {
            result = opener.Open(url);
        }
        catch (Exception)
        {
            // Any opener failure (a missing program, a platform error) shows the same notice.
            result = new UrlOpenResult(false, OpenerFailed);
        }

        dispatcher.Post(new IssueLinkOpenFinished(url, result.Opened, result.Error));
    }
}
