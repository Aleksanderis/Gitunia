namespace Gitunia.Core.Keymap;

/// <summary>Canonical names of the non-printable keys a <see cref="KeyEvent"/> can carry.</summary>
public static class KeyNames
{
    public const string Enter = "Enter";
    public const string Escape = "Esc";
    public const string Tab = "Tab";
    public const string Backspace = "Backspace";
    public const string Delete = "Delete";
    public const string Up = "Up";
    public const string Down = "Down";
    public const string Left = "Left";
    public const string Right = "Right";
    public const string Home = "Home";
    public const string End = "End";
    public const string PageUp = "PageUp";
    public const string PageDown = "PageDown";

    private static readonly Dictionary<string, string> CanonicalByLowerName = BuildNames();

    /// <summary>True when the name is a canonical named key rather than typed text.</summary>
    public static bool IsNamed(string name) => CanonicalByLowerName.TryGetValue(name.ToLowerInvariant(), out var canonical) && canonical == name;

    /// <summary>Finds the canonical name for a case-insensitive key name.</summary>
    public static bool TryGetCanonical(string name, out string canonical)
    {
        return CanonicalByLowerName.TryGetValue(name.ToLowerInvariant(), out canonical!);
    }

    private static Dictionary<string, string> BuildNames()
    {
        var names = new List<string> { Enter, Escape, Tab, Backspace, Delete, Up, Down, Left, Right, Home, End, PageUp, PageDown };
        for (var number = 1; number <= 12; number++)
        {
            names.Add($"F{number}");
        }

        return names.ToDictionary(name => name.ToLowerInvariant(), name => name);
    }
}
