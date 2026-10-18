namespace Gitunia.Tui.Tests.Loading;

/// <summary>A clock tests move by hand, one tick per millisecond; the UTC time follows the same ticks.</summary>
public sealed class ManualTimeProvider : TimeProvider
{
    private static readonly DateTimeOffset Epoch = new(2026, 10, 18, 12, 0, 0, TimeSpan.Zero);

    private long _timestamp;

    public override long TimestampFrequency => 1000;

    public override long GetTimestamp() => _timestamp;

    public override DateTimeOffset GetUtcNow() => Epoch.AddMilliseconds(_timestamp);

    public void Advance(TimeSpan span) => _timestamp += (long)span.TotalMilliseconds;
}
