namespace Gitunia.Tui.Tests.Loading;

/// <summary>A clock tests move by hand, one tick per millisecond.</summary>
public sealed class ManualTimeProvider : TimeProvider
{
    private long timestamp;

    public override long TimestampFrequency => 1000;

    public override long GetTimestamp() => timestamp;

    public void Advance(TimeSpan span) => timestamp += (long)span.TotalMilliseconds;
}
