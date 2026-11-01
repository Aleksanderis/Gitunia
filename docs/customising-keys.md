# Customising keys

Key bindings are `keys` entries in `config.json`. Defaults are in the [keymap reference](keymap.md); the file format is in [ux-look.md](ux-look.md).

- **File:** `~/.config/gitunia/config.json` (`$XDG_CONFIG_HOME/gitunia/config.json` if set). Settings (`<space>,`) opens it in `$EDITOR`. JSON with comments; create it with `{}` if missing.
- **Entry fields:** `action` (action id, or `none` to unbind), `key` (key or chord, `<space>` is the leader), `view` (default `Global`; names are the keymap headings), `mode` (`Normal` default, `Visual`, `Insert`, `Command`, `Confirm`). One entry binds one view and one mode.

```jsonc
{
  "keys": [
    // Bind a free key.
    { "view": "Graph", "key": "J", "action": "list.down" },
    // Replace what a key does.
    { "key": "q", "action": "palette.open" },
    // Unbind.
    { "view": "Lists", "key": "gg", "action": "none" }
  ]
}
```

A new entry replaces the existing binding of that key in that view and mode. To move an action, bind the new key and unbind the old one.

**Find an action id.** Settings, category Keys, row Key reference: `/` filters, and `Enter` on a line shows its `keys` entry in the status bar. The command palette (`:`) lists actions by title.

- **Conflicts:** a key cannot be both a binding and the start of a longer chord (`g` while `gg` exists is refused). The message names both keys; unbind the longer ones first. `Ctrl-c` always quits.
- **Restart:** the keymap is read at startup; restart after editing.
- **Bad entries:** a bad entry is skipped and the rest apply; the first problem shows in the status bar and `<space>m` keeps the messages. Invalid JSON means defaults apply.
- **Check:** `?` lists your own bindings under `Custom`.
