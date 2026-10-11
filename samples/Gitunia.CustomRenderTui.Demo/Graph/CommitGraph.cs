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
            var index = Layout.Count;
            var parentCount = 0;
            var rest = Store.Field(index, 1);
            while (rest.Length > 0 && parentCount < parents.Length)
            {
                var spaceIndex = rest.IndexOf((byte)' ');
                parents[parentCount++] = HashKey(spaceIndex < 0 ? rest : rest[..spaceIndex]);
                rest = spaceIndex < 0 ? [] : rest[(spaceIndex + 1)..];
            }

            Layout.Add(HashKey(Store.Field(index, 0)), parents[..parentCount]);
        }
    }

    /// <summary>First 15 hex digits as a number: unique enough as a lane key (never 0, which means "empty lane").</summary>
    private static ulong HashKey(ReadOnlySpan<byte> hex)
    {
        ulong key = 0;
        for (var i = 0; i < Math.Min(15, hex.Length); i++)
        {
            var character = hex[i];
            var digit = character <= (byte)'9' ? (uint)(character - '0') : (uint)((character | 0x20) - 'a' + 10);
            key = (key << 4) | digit;
        }

        return key == 0 ? 1 : key;
    }
}
