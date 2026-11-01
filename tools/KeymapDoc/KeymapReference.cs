using System.Text;
using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;

namespace Gitunia.KeymapDoc;

/// <summary>
/// Renders <c>docs/keymap.md</c> from the rows of <c>keymap.default.json</c>. Shared by the generator program and the
/// test that fails when the committed file is stale (the test project links this file). Rows are read with
/// <see cref="KeymapRowReader"/>, which skips properties it does not know, so new fields in the file do not break it.
/// </summary>
public static class KeymapReference
{
    public static string Render(IReadOnlyList<KeymapRow> rows)
    {
        var text = new StringBuilder();
        text.Append("# Keymap reference\n\n");
        text.Append("<!-- Generated from src/Gitunia.Core/Keymap/keymap.default.json by tools/KeymapDoc. Do not edit: change the JSON, then run `scripts/gen-keymap-doc.sh`. A test fails when this file is stale. -->\n\n");
        text.Append("Every default key binding, one table per view. The design rules, notation and modes are in [ux-keymap.md](internal/ux-keymap.md). ");
        text.Append("A view's keys also apply to the views that inherit from it (`Global` applies everywhere). Keys are listed main key first; ");
        text.Append("`Ctrl-w c` is a chord, `<space>o` a leader chord. Rebinds in `config.json` are not shown here (see the Key reference in the settings screen).\n");

        var views = rows.Select(row => row.View).Distinct(StringComparer.Ordinal).ToList();
        foreach (var view in views)
        {
            text.Append($"\n## {view}\n\n| Keys | Modes | Action | What |\n|---|---|---|---|\n");
            foreach (var row in rows.Where(row => row.View == view))
            {
                var keys = string.Join(' ', row.Keys.Select(Code));
                var modes = string.Join(", ", row.Modes);
                text.Append($"| {keys} | {modes} | `{row.Action.Value}` | {Escape(ActionCatalog.Describe(row.Action))} |\n");
            }
        }

        var bound = rows.Select(row => row.Action).ToHashSet();
        var unbound = ActionCatalog.NoDefaultKey.Where(action => !bound.Contains(action)).Select(action => action.Value).Order(StringComparer.Ordinal).ToList();
        if (unbound.Count > 0)
        {
            text.Append("\n## Actions without a key\n\nNo default key; reached from the command palette (`:`), the actions menu, the mouse or a prompt.\n\n| Action | What |\n|---|---|\n");
            foreach (var id in unbound)
            {
                text.Append($"| `{id}` | {Escape(ActionCatalog.Describe(new ActionId(id)))} |\n");
            }
        }

        return text.ToString();
    }

    private static string Escape(string value) => value.Replace("|", "\\|", StringComparison.Ordinal);

    // A key containing a backtick needs a wider code span; a pipe is escaped inside a table cell.
    private static string Code(string key)
    {
        var escaped = key.Replace("|", "\\|", StringComparison.Ordinal);
        return key.Contains('`', StringComparison.Ordinal) ? $"`` {escaped} ``" : $"`{escaped}`";
    }
}
