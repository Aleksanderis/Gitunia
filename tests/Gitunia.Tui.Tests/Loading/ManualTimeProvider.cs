namespace Gitunia.Tui.Tests.Loading;

/// <summary>A clock tests move by hand, one tick per millisecond.</summary>
public sealed class ManualTimeProvider : TimeProvider
{
    private long _timestamp;

    public override long TimestampFrequency => 1000;

    public override long GetTimestamp() => _timestamp;

    public void Advance(TimeSpan span) => _timestamp += (long)span.TotalMilliseconds;
}
