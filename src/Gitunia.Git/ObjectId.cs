using System.Buffers.Binary;

namespace Gitunia.Git;

/// <summary>
/// A git object id (SHA-1 or SHA-256) stored in a fixed 40-byte struct instead of a 40 or 64 character string,
/// so 100k commits and their parent links stay compact without an intern table.
/// </summary>
public readonly struct ObjectId : IEquatable<ObjectId>
{
    private readonly ulong _first;
    private readonly ulong _second;
    private readonly ulong _third;
    private readonly ulong _fourth;
    private readonly byte _byteCount;

    private ObjectId(ReadOnlySpan<byte> bytes)
    {
        Span<byte> padded = stackalloc byte[32];
        bytes.CopyTo(padded);
        _first = BinaryPrimitives.ReadUInt64LittleEndian(padded);
        _second = BinaryPrimitives.ReadUInt64LittleEndian(padded[8..]);
        _third = BinaryPrimitives.ReadUInt64LittleEndian(padded[16..]);
        _fourth = BinaryPrimitives.ReadUInt64LittleEndian(padded[24..]);
        _byteCount = (byte)bytes.Length;
    }

    /// <summary>Whether this is a real id (the default value is not).</summary>
    public bool IsValid => _byteCount != 0;

    /// <summary>A 64-bit mix of every byte of the id, for building fingerprints (not a stable format; do not persist it).</summary>
    public ulong Hash64 => _first ^ (_second * 0x9E3779B97F4A7C15UL) ^ (_third * 0xC2B2AE3D27D4EB4FUL) ^ (_fourth * 0x165667B19E3779F9UL);

    /// <summary>Parses 40 (SHA-1) or 64 (SHA-256) hex digits given as ASCII bytes.</summary>
    public static bool TryParse(ReadOnlySpan<byte> hexDigits, out ObjectId objectId)
    {
        objectId = default;
        if (hexDigits.Length is not (40 or 64))
        {
            return false;
        }

        Span<byte> bytes = stackalloc byte[32];
        var byteLength = hexDigits.Length / 2;
        for (var index = 0; index < byteLength; index++)
        {
            var high = HexValue(hexDigits[index * 2]);
            var low = HexValue(hexDigits[(index * 2) + 1]);
            if (high < 0 || low < 0)
            {
                return false;
            }

            bytes[index] = (byte)((high << 4) | low);
        }

        objectId = new ObjectId(bytes[..byteLength]);
        return true;
    }

    /// <summary>Parses 40 (SHA-1) or 64 (SHA-256) hex digits given as characters.</summary>
    public static bool TryParse(ReadOnlySpan<char> hexDigits, out ObjectId objectId)
    {
        Span<byte> ascii = stackalloc byte[Math.Min(hexDigits.Length, 64)];
        if (hexDigits.Length > 64)
        {
            objectId = default;
            return false;
        }

        for (var index = 0; index < hexDigits.Length; index++)
        {
            ascii[index] = (byte)hexDigits[index];
        }

        return TryParse(ascii, out objectId);
    }

    public static bool TryParse(string hexDigits, out ObjectId objectId) => TryParse(hexDigits.AsSpan(), out objectId);

    public static bool operator ==(ObjectId left, ObjectId right) => left.Equals(right);

    public static bool operator !=(ObjectId left, ObjectId right) => !left.Equals(right);

    public bool Equals(ObjectId other) =>
        _first == other._first && _second == other._second && _third == other._third && _fourth == other._fourth && _byteCount == other._byteCount;

    public override bool Equals(object? obj) => obj is ObjectId other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(_first, _second, _third, _fourth);

    /// <summary>Lowercase hex, the form git prints.</summary>
    public override string ToString()
    {
        Span<byte> bytes = stackalloc byte[32];
        BinaryPrimitives.WriteUInt64LittleEndian(bytes, _first);
        BinaryPrimitives.WriteUInt64LittleEndian(bytes[8..], _second);
        BinaryPrimitives.WriteUInt64LittleEndian(bytes[16..], _third);
        BinaryPrimitives.WriteUInt64LittleEndian(bytes[24..], _fourth);
        return Convert.ToHexStringLower(bytes[.._byteCount]);
    }

    private static int HexValue(byte digit) => digit switch
    {
        >= (byte)'0' and <= (byte)'9' => digit - '0',
        >= (byte)'a' and <= (byte)'f' => digit - 'a' + 10,
        >= (byte)'A' and <= (byte)'F' => digit - 'A' + 10,
        _ => -1,
    };
}
