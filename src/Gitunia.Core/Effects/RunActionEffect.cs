namespace Gitunia.Core.Effects;

/// <summary>Run a callback of the terminal retry flow, the one caller left (see <c>RunActionCommand</c>).</summary>
/// <param name="Run">What to run, on the UI thread.</param>
public sealed record RunActionEffect(Action Run) : AppEffect;
