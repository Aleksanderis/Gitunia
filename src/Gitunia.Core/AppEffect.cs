namespace Gitunia.Core;

/// <summary>
/// Work the runner performs after a state change. Effects are coarse data (one git operation or one external action)
/// and carry the repository location they run against, never the process working directory.
/// </summary>
public abstract record AppEffect
{
    private protected AppEffect()
    {
    }
}
