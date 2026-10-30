using Gitunia.Core.Settings;
using Gitunia.Core.Settings.Models;
using Gitunia.Core.Tests.Keymap;
using Xunit;

namespace Gitunia.Core.Tests.Settings;

public class SettingsDocumentationTests
{
    private const string SectionHeading = "## Settings screen";

    [Fact]
    public void GivenTheUxSettingsTable_WhenComparedWithTheRegistry_ThenEverySettingIsDocumented()
    {
        var rows = ReadTable();

        foreach (var definition in SettingsRegistry.All)
        {
            var row = rows.SingleOrDefault(candidate => candidate[1] == definition.Name);
            Assert.True(row is not null, $"{definition.Name} is not in the settings table of docs/ux-settings.md");
            Assert.Equal(SettingsRegistry.TitleOf(definition.Category), row[0]);
            Assert.Contains(ControlWord(definition.Control), row[2], StringComparison.Ordinal);
            var key = definition.ConfigKey.Length == 0 ? "none" : $"`{definition.ConfigKey}`";
            Assert.Equal(key, row[3]);
        }
    }

    private static string ControlWord(SettingControl control) => control switch
    {
        SettingControl.Toggle => "toggle",
        SettingControl.Choice => "choice",
        SettingControl.Number => "number",
        SettingControl.PathList => "path list",
        SettingControl.KeyReference => "read-only list",
        _ => "action",
    };

    private static List<string[]> ReadTable()
    {
        var rows = new List<string[]>();
        var inSection = false;
        foreach (var line in File.ReadAllLines(UxKeymapReader.FindUxDocument("ux-settings.md")))
        {
            if (line.StartsWith("## ", StringComparison.Ordinal))
            {
                inSection = line.StartsWith(SectionHeading, StringComparison.Ordinal);
                continue;
            }

            if (!inSection || !line.StartsWith('|'))
            {
                continue;
            }

            var cells = line.Trim().Trim('|').Split('|').Select(cell => cell.Trim()).ToArray();
            if (cells.Length >= 4 && cells[0] != "Category" && !cells[0].StartsWith("---", StringComparison.Ordinal))
            {
                rows.Add(cells);
            }
        }

        return rows;
    }
}
