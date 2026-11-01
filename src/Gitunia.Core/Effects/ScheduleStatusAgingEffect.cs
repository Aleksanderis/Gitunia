namespace Gitunia.Core.Effects;

/// <summary>Post a <c>StatusAgedCommand</c> after the delay, because the shown message changes its look then
/// (a newer schedule replaces an older one).</summary>
/// <param name="After">The time until the message loses its highlight or expires.</param>
public sealed record ScheduleStatusAgingEffect(TimeSpan After) : AppEffect;
