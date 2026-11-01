namespace Gitunia.Core.Text;

/// <summary>Text made safe to hand to a terminal.</summary>
public static class PrintableText
{
    private const char ReplacementCharacter = '�';

    /// <summary>The text with every lone surrogate replaced by the replacement character, so a terminal never gets a
    /// broken string.</summary>
    /// <param name="text">The text.</param>
    public static string Of(string text)
    {
        if (!text.Any(char.IsSurrogate))
        {
            return text;
        }

        var characters = text.ToCharArray();
        for (var index = 0; index < characters.Length; index++)
        {
            if (char.IsHighSurrogate(characters[index])
                && index + 1 < characters.Length
                && char.IsLowSurrogate(characters[index + 1]))
            {
                index++;
            }
            else if (char.IsSurrogate(characters[index]))
            {
                characters[index] = ReplacementCharacter;
            }
        }

        return new string(characters);
    }
}
