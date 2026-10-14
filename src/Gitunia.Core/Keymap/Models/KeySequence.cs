using System.Collections.Immutable;

namespace Gitunia.Core.Keymap.Models;

/// <summary>An ordered list of keys that together trigger a binding (a chord when longer than one key).</summary>
public sealed class KeySequence : IEquatable<KeySequence>
{
    public static readonly KeySequence Empty = new([]);

    public KeySequence(IEnumerable<KeyEvent> keys)
    {
        Keys = [.. keys.Select(key => key.Normalize())];
    }

    public ImmutableArray<KeyEvent> Keys { get; }

    public int Length => Keys.Length;

    public KeySequence Append(KeyEvent key) => new(Keys.Add(key));

    /// <summary>True when this sequence begins with every key of <paramref name="prefix"/> (equal sequences count).</summary>
    public bool StartsWith(KeySequence prefix)
    {
        return prefix.Length <= Length && Keys.Take(prefix.Length).SequenceEqual(prefix.Keys);
    }

    public bool Equals(KeySequence? other) => other is not null && other.Length == Length && StartsWith(other);

    public override bool Equals(object? obj) => Equals(obj as KeySequence);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var key in Keys)
        {
            hash.Add(key);
        }

        return hash.ToHashCode();
    }

    public override string ToString() => string.Concat(Keys.Select(key => key.ToString()));
}
