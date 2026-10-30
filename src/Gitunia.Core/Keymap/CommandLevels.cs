using Gitunia.Core.Keymap.Models;

namespace Gitunia.Core.Keymap;

/// <summary>Names and cycling of the command levels (#839).</summary>
public static class CommandLevels
{
    /// <summary>The level the product starts with.</summary>
    public const CommandLevel Default = CommandLevel.Everyday;

    /// <summary>
    /// The names as written in <c>config.json</c> and listed by the settings row, lowest level first.
    /// </summary>
    public static IReadOnlyList<string> Names { get; } = ["everyday", "advanced", "advanced+"];

    /// <summary>The config name of <paramref name="level"/>.</summary>
    public static string NameOf(CommandLevel level) => level switch
    {
        CommandLevel.Advanced => "advanced",
        CommandLevel.AdvancedPlus => "advanced+",
        _ => "everyday",
    };

    /// <summary>The label shown to the user (<c>Everyday</c>, <c>Advanced</c>, <c>Advanced+</c>).</summary>
    public static string LabelOf(CommandLevel level) => level switch
    {
        CommandLevel.Advanced => "Advanced",
        CommandLevel.AdvancedPlus => "Advanced+",
        _ => "Everyday",
    };

    /// <summary>The level a config name stands for (any case); false when it is none.</summary>
    public static bool TryParse(string? text, out CommandLevel level)
    {
        level = Default;
        if (text is null)
        {
            return false;
        }

        for (var index = 0; index < Names.Count; index++)
        {
            if (string.Equals(text, Names[index], StringComparison.OrdinalIgnoreCase))
            {
                level = (CommandLevel)index;

                return true;
            }
        }

        return false;
    }

    /// <summary>The level after <paramref name="level"/>: Everyday, Advanced, Advanced+, then Everyday again.</summary>
    public static CommandLevel Next(CommandLevel level) => level switch
    {
        CommandLevel.Everyday => CommandLevel.Advanced,
        CommandLevel.Advanced => CommandLevel.AdvancedPlus,
        _ => CommandLevel.Everyday,
    };
}
