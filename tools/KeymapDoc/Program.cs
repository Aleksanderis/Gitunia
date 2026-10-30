// Writes docs/keymap.md from keymap.default.json.
// Usage: KeymapDoc --json <keymap.default.json> --out <docs/keymap.md>
using Gitunia.Core.Keymap;
using Gitunia.KeymapDoc;

string? jsonPath = null;
string? outPath = null;
for (var index = 0; index < args.Length - 1; index++)
{
    if (args[index] == "--json")
    {
        jsonPath = args[index + 1];
    }

    if (args[index] == "--out")
    {
        outPath = args[index + 1];
    }
}

if (jsonPath is null || outPath is null)
{
    Console.Error.WriteLine("Usage: KeymapDoc --json <keymap.default.json> --out <keymap.md>");
    return 2;
}

var rows = KeymapRowReader.Read(File.ReadAllBytes(jsonPath));
File.WriteAllText(outPath, KeymapReference.Render(rows));
return 0;
