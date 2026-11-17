using Gitunia.Core.Prompts;
using Xunit;

namespace Gitunia.Core.Tests.Prompts;

/// <summary>Reads what a feature reducer returns for a rejected prompt answer.</summary>
public static class PromptRejections
{
    /// <summary>The reason of the one effect that reopens the prompt; fails when the answer was not rejected that way.</summary>
    /// <param name="effects">What the reducer returned.</param>
    public static string Reason(IEnumerable<AppEffect> effects) =>
        Assert.IsType<ReopenPromptEffect>(Assert.Single(effects)).Reason;
}
