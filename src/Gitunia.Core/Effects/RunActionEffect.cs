namespace Gitunia.Core.Effects;

/// <summary>Run a callback of a controller that has not moved to commands yet (see <c>RunActionCommand</c>).</summary>
/// <param name="Run">What to run, on the UI thread.</param>
public sealed record RunActionEffect(Action Run) : AppEffect;
