using Gitunia.Git.Execution.Exceptions;
using Gitunia.Core.Text;
using Xunit;

namespace Gitunia.Core.Tests.Text;

public class RemoteFailureTextTests
{
    [Fact]
    public void GivenARejectedPushWithProgressAndUrlLines_WhenReading_ThenTheRejectionLineIsTheReason()
    {
        var stderr = "Enumerating objects: 5, done.\rCounting objects: 100% (5/5)\r\nTo host:repo.git\n"
            + " ! [rejected]        main -> main (fetch first)\nerror: failed to push some refs\n";
        var exception = new GitCommandFailedException(["push"], 1, stderr);

        Assert.Equal("! [rejected]        main -> main (fetch first)", RemoteFailureText.Reason(exception));
    }

    [Fact]
    public void GivenAFatalLineAfterNoise_WhenReading_ThenTheFatalLineIsTheReason()
    {
        var exception = new GitCommandFailedException(["fetch"], 128, "remote: hi\nfatal: unable to access 'x'\n");

        Assert.Equal("fatal: unable to access 'x'", RemoteFailureText.Reason(exception));
    }

    [Fact]
    public void GivenNoRecognisedLine_WhenReading_ThenTheLastLineIsUsed()
    {
        var exception = new GitCommandFailedException(["fetch"], 1, "first\nlast\n");

        Assert.Equal("last", RemoteFailureText.Reason(exception));
        Assert.Equal("unknown error", RemoteFailureText.Reason(new InvalidOperationException(string.Empty)));
    }
}
