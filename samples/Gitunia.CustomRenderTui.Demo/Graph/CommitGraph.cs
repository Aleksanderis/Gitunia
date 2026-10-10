namespace Gitunia.CustomRenderTui.Demo.Graph;

/// <summary>Streamed commit lines plus their lane layout; appended to on the UI thread only.</summary>
public sealed class CommitGraph
{
    public CommitStore Store { get; } = new();

    public LaneLayout Layout { get; } = new();

    public int Count => Layout.Count;

    public void Append(ReadOnlySpan<byte> chunk)
    {
        Store.Append(chunk);
        Span<ulong> parents = stackalloc ulong[8];
        while (Layout.Count < Store.Count)
        {
            var i = Layout.Count;
            var n = 0;
            var rest = Store.Field(i, 1);
            while (rest.Length > 0 && n < parents.Length)
            {
                var sp = rest.IndexOf((byte)' ');
                parents[n++] = HashKey(sp < 0 ? rest : rest[..sp]);
                rest = sp < 0 ? [] : rest[(sp + 1)..];
            }

            Layout.Add(HashKey(Store.Field(i, 0)), parents[..n]);
        }
    }

    /// <summary>First 15 hex digits as a number: unique enough as a lane key (never 0, which means "empty lane").</summary>
    private static ulong HashKey(ReadOnlySpan<byte> hex)
    {
        ulong v = 0;
        for (var i = 0; i < Math.Min(15, hex.Length); i++)
        {
            var c = hex[i];
            uint digit = c <= (byte)'9' ? (uint)(c - '0') : (uint)((c | 0x20) - 'a' + 10);
            v = (v << 4) | digit;
        }

        return v == 0 ? 1 : v;
    }
}
