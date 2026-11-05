# UX: look, colours and config file

*Internal design note for maintainers and agents. Not needed to use or contribute to Gitunia.*

Part of the UX spec; start at [ux.md](ux.md).

## Look and colours

Colours are decided in one place: `PaletteTable` (`Gitunia.Tui.Views`) holds the colours of a scheme and `Palette` says which role uses which colour, so a theme swaps the table and nothing else. The config file's `theme` setting picks the scheme: `auto` (Catppuccin Mocha or Latte by the terminal's background) or the name of a theme file ("Theme files" below). `dark` and `light` are still accepted as the built-in dark and light tables, close to but not identical with the `catppuccin-mocha` and `catppuccin-latte` files; the settings screen shows them as those two themes and keeping the option rewrites the value to the file name.

**Frame.** Every pane is a box and neighbouring boxes share their edges, so the whole screen is one frame split into panes (`FrameGrid`, drawn the same way by the UI and by `--print-frame`):

```
 1 gitunia                                                   <- tab row
╭─ gitunia ────────────┬─ History ──────────────────────────────╮
│  ▾ Branches 2        │ ○╲  main Merge feature x       Ada ... │
│     feature/x        │ ●  │  Fix parser                       │
├─ Details ─ Changes ─ Files ─ 64d72f2 ─ 3 files ┬─ Diff ... ──┤
│ commit  64d72f2...                             │ @@ -1 +1 @@  │
╰────────────────────────────────────────────────┴─────────────╯
 NORMAL  1/4  j/k gg G Ctrl-d Ctrl-u ? : q                          <- status bar
```

- Rounded outer corners and plain T and cross junctions; the ASCII tier uses `+`, `-` and `|`. Where two boxes meet there is one line, not two.
- Titles sit in the top edge of each box as chips: the sidebar shows the repository name, the graph `History`, the bottom pane its tabs, the commit and the file count, the diff its title and buttons. The bottom pane's title row is the line between the graph and the bottom panes.
- From 100 columns of screen width the History list and the bottom detail and diff panes are padded by two columns (`PanePadding.SpaciousColumns`; the graph on its right side only, its first lane starts next to the frame), the sidebar and narrower screens keep one; the first and last column and the top and bottom edge row belong to the frame.
- Focus: the focused pane's whole box is drawn in the accent colour (bold), every other line in the quiet border colour, and its title chip is the filled accent chip (the others are dim chips). Focus never relies on colour alone: with `NO_COLOR` the focused box is bold.
- Layout numbers: the sidebar (clamped 20-36 columns, shown from 60 columns) and the detail/diff split are widths including the padding; drag and the `Ctrl-w` size keys move the shared line.

**Selection and focus.** The selected row is a background tint, never inverse video, so the row keeps its own colours. The focused pane's selection uses the full tint (blue), a pane without focus a quieter grey tint. At 24-bit the focused bar is a clear step from the page (#934): about 1.8:1 against the dark base (`#45475a` on `#1e1e2e`) and about 1.4:1 against the light base (`#c9ced9` on `#eff1f5`); the other dark and light built-in themes follow it (Tokyo Night Storm, Moon and Day, Catppuccin Frappe, #935: about 1.8:1 dark, 1.4:1 light), with text, accent and lane colours nudged lighter or darker where the bar would otherwise cost them their ratio; Solarized Dark stops at about 1.6:1 because a stronger bar would need its text lightened further from the canonical palette (its default text is already lightened for this, #935); the unfocused tint stays near the page. Text, muted and subtle text keep their ratios on it (4.5 and 4.5/2.0), refs, lanes and other accent-coloured text need 3.5 there (a bar that visible cannot also give accents 4.5), and a test checks all of it. The Visual range tint is blended from the bar, so it stays the strongest of the three (a smaller accent share in light themes keeps text readable). The rows of a Visual range are drawn stronger than the selected row: a background one step further towards the accent at 24-bit colour, bold text in every tier, plus reverse video at 16 colours and `NO_COLOR`; the moving end (the cursor row) is also underlined, and a range looks the same whether the pane has focus or not; a changed word in the diff keeps its tint blended 75% toward the range tint, so selected characters stay apart from the word around them (#1176). The active tab, the status mode (`NORMAL`, `INSERT`...) and an overlay's title bar are the same accent chip; the tab row and the status bar sit on a slightly lighter bar; an overlay's frame is accent.

**Repository picker (#880, #1203, #1204, #1205).** A framed overlay like the palette: accent title bar (`Open repository` / `Locate repository`) with a right-aligned count (`11 repositories`, `directories` for a typed path; dropped when the box is too narrow), a query line (`›` prompt, the typed text with its cursor cell or a faint placeholder, and the hint `Tab accepts a path` right-aligned when it fits, the key in the chip key role; the key is that of `picker.complete-path` in the keymap, `Tab` by default, and the hint is left out when the action has none), a blank line, the rows, the error notice, a faint rule and the footer row of chips: left `Enter open` (`Enter locate` while locating a missing repository), `Alt-p pin`/`unpin`, `Alt-d remove` (for the highlighted row), right `Alt-c clone`, `Alt-i init`, `Esc close` (a chip that does not fit is left out, the right group from its left end). The box is at most 100 columns and 20 list lines (headers, spacers and more lines count), centred, and shrinks with the list; below 30 columns or 7 rows it is drawn without a frame (query, rows, notice; no spacers, no hint). Unfiltered lists with more than one source (or a capped one) get group headers: the name in capitals (section-title role) and a rule to the right edge (faint role), with one blank spacer line above each header except the first (a list line that is not a row). Each section is capped (open 6, pinned 6, recent 5, found 3 rows; `PickerListPlan`): a capped header carries the shown count in the faint role (`RECENT  5 of 31`) and the section ends with a selectable line under the glyph column, `▸  26 more` (faint, `>` in ASCII); an expanded section lists every row and ends with `▴  less` (`^`), without a count in its header. On a short terminal the caps shrink (found first, then pinned, open and last recent) down to two rows per section, each keeping its more line; below that the list scrolls. A query or a typed path lists the rows flat, without headers, caps or more lines. While a more or less line is highlighted the footer's first chip reads `Enter expand` or `Enter collapse` and the pin and remove chips are left out. Row: one glyph, two spaces, the name (plain, at most 24 columns, a directory that is not a repository ends in the path separator), the checked-out branch of an open repository (current-branch cyan, at most 20 columns; read from the tab's state, so no git call, and empty on recent, pinned-only, found, missing and detached rows; the whole column is dropped when keeping it would shorten the longest path) and the path right-aligned, muted, home as `~`, cut in the middle (`~/…/aa2`) when it does not fit; every row keeps the last two columns free, and a pinned row whose glyph is not `★` (open or missing) shows `★` there. The glyph is the first that applies and has its own existing role: `!` missing (warning yellow), `●` open (local-branch green), `★` pinned (current-branch cyan), `◷` recent (remote purple), `○` found or directory (muted); the ASCII set uses `!`, `*`, `+`, `~`, `o` and `>` for the prompt (`PickerGlyphs`, in `GraphGlyphSet`). The status bar shows only the mode chip and the highlight position `n/m` while the picker is open (the keys are in the box). No new palette roles.

**Roles to colours** (`PaletteTable` has the values): accent blue for section titles, buttons, diff file headers and the focused frame (bold); graph lanes one of 12 colours per branch (6 at 16 colours, none with `NO_COLOR`), commit marks in the lane's colour, branch labels a bold chip filled with the branch colour (text in the theme's chip colour when it reads on it, else black or white; inverse video without colours); local branch green, current branch cyan, remote branch purple, tag yellow (remote branches are not red: red means an error or a deletion); hash, date, line numbers, hints and counts muted; diff whitespace marks subtle (muted blended into the background; faint at 16 colours and `NO_COLOR`); changed words the line's colour on a tint of it (`addedWord` / `removedWord`, bold; at 16 colours and `NO_COLOR` bold and underlined); added / removed / modified green / red / yellow, hunk header cyan; errors red bold; search match dark text on yellow; overlay key column yellow bold.

**Colour depth.** The scheme follows the terminal. 24-bit (`COLORTERM=truecolor`, also used for 256-colour terminals because Terminal.Gui has no 256-colour output of its own): the Catppuccin palette, checked by tests for a contrast ratio of at least 4.5 on a typical dark and light terminal background (3.0 for dividers), also on selected rows. 16-colour (`TERM` without `256color`, or a driver that cannot do 24-bit; a terminal that sets no `COLORTERM`, for example over ssh, gets it: set `COLORTERM=truecolor` to force 24-bit): only the 16 named colours, so the terminal's own theme decides the exact look; the focused selection is bright black (bold; blue in the classic Windows console), the unfocused one underlined. `NO_COLOR` or `TERM=dumb`: text styles only (reverse video for the selection, underline when unfocused, bold underline for search matches, underline for the current branch).
With `theme = auto` the terminal is asked for its background (OSC 11, before the UI starts, 50 ms timeout, skipped when not interactive, at `NO_COLOR`, for `TERM` unset, `dumb` or `linux`, and on Windows); an unanswered query falls back to `COLORFGBG`, and a terminal that offers neither is assumed dark. The `theme` setting overrides that detection (`light` or `dark`; a theme file's `variant` does the same) at every colour depth except `NO_COLOR`; it tells Gitunia what the terminal looks like, so `light` at 16 colours (where the terminal's own background is kept) is only readable on a light terminal, while at 24 bits the scheme paints its own backgrounds and works on either.

**Theme files (#61).** A theme is a JSON file (comments and trailing commas allowed), so a user adds one without a release.
Built-in themes are the same files, embedded in the binary: `catppuccin-mocha`, `catppuccin-macchiato`, `catppuccin-frappe`, `catppuccin-latte`, `tokyo-night`, `tokyo-night-storm`, `tokyo-night-moon`, `tokyo-night-day`, `gruvbox-dark-hard`, `gruvbox-dark-medium`, `gruvbox-dark-soft`, `gruvbox-light-hard`, `gruvbox-light-medium`, `gruvbox-light-soft`, `dracula`, `nord`, `solarized-dark`, `solarized-light`, `rose-pine`, `rose-pine-moon`, `rose-pine-dawn` (#610).
A user file is `themes/<name>.json` in the config directory; its lower-case file name is the value of `theme`, and it wins over a built-in theme of the same name.
`auto`, `dark` and `light` are reserved.
A file that is unreadable or invalid is not offered, and each reason is reported as a config problem at startup (a `theme` naming it falls back to `auto`).
The files are read and parsed once, at startup; edits show after a restart.

```jsonc
{
  "version": 1,                 // optional; the only format there is, so a later change cannot break old files
  "name": "My Theme",           // optional, defaults to the file name
  "variant": "dark",            // required: dark or light, the side of the terminal the theme is made for
  "colors": {                   // "#rrggbb" only
    // required
    "accent": "#89b4fa", "good": "#a6e3a1", "bad": "#f38ba8", "remote": "#cba6f7", "warn": "#f9e2af", "info": "#89dceb",
    "muted": "#9399b2", "border": "#6c7086", "barBackground": "#181825",
    "selectionFocused": "#45475a", "selectionDim": "#262637",
    "chipForeground": "#11111b", "baseForeground": "#cdd6f4", "baseBackground": "#1e1e2e",
    "lanes": ["#89b4fa", "#a6e3a1", "#f9e2af"],            // the branch colours of the graph, by slot; at least one
    // optional (the default is in parentheses)
    "mutedOnSelection": "#bac2de",    // (muted)
    "subtle": "#5e6277",              // (muted blended 55% into baseBackground: diff whitespace marks)
    "subtleOnSelection": "#8388a0",   // (mutedOnSelection blended 70% into selectionFocused)
    "groupBackground": "#222232",     // (none: the cursor group is marked by its bar only)
    "tabButtonBackground": "#313244", // (selectionFocused; also the background of the selected bottom-pane tab)
    "chipBackground": "#89b4fa",      // (accent)
    "matchForeground": "#11111b",     // (chipForeground)
    "matchBackground": "#f9e2af",     // (warn)
    "addedWord": "#323c3f",           // (good blended into baseBackground, as strongly as the text stays readable)
    "removedWord": "#3e2e40"          // (bad blended into baseBackground likewise)
  },
  // Optional 16-colour mapping, see below.
  "ansi": { "accent": "brightBlue" }
}
```

The built-in values are the published palettes (the Gruvbox, Dracula, Nord, Solarized and Rosé Pine files use the published hex values, all MIT-licensed), except that roles which must meet the contrast tests are moved the least that does it. Roles are the `PaletteTable` colours. Unknown keys are ignored; a missing required role, a malformed colour, an unknown `variant` or `version`, or a bad `ansi` entry refuses the whole file, one problem per reason. The colours are used at 24-bit and 256-colour output, and the base text and background are always painted, so the theme looks the same on any terminal background.

**The 16-colour mapping rule.** At 16 colours the terminal's own palette decides what the named colours look like, so a theme cannot carry its hex values there. The `variant` picks the built-in 16-colour table (dark: bright accents; light: normal-intensity accents), and the optional `ansi` object overrides single roles (`accent`, `good`, `bad`, `remote`, `warn`, `info`, `muted`, `mutedOnSelection`, `border`, `chipBackground`) with one of `black red green yellow blue magenta cyan gray darkGray brightRed brightGreen brightYellow brightBlue brightMagenta brightCyan white` (case-insensitive). Selection and lanes keep the variant's choice; a theme whose `variant` differs from the detected terminal background paints black or white as base. Contrast tests cover every built-in theme at 24-bit, after rounding to the xterm 256-colour palette and at 16 colours; a user theme is not checked.

**Looking at it without the UI.** `gitunia <repository> --print-frame WxH [--diff] --colors truecolor|16|none [--light]` writes the frame with ANSI colours (reads `config.json` like the app). Overlays and the picker are not part of it; open them with `?`, `:` and `Ctrl-t`.

## Config file

`config.json` in the config directory (`$XDG_CONFIG_HOME/gitunia` or `~/.config/gitunia`, see `architecture.md`).
JSON with comments and trailing commas, read with source-generated System.Text.Json.
Unknown properties are ignored, so later sections fit beside `keys`.
The file is read once at startup and never modified by the loader; the settings screen ([ux-settings.md](ux-settings.md)) is the only writer.

```jsonc
{
  // auto (default: follow the terminal's background) or a theme file name such as "catppuccin-mocha" or "tokyo-night".
  // The older values dark and light still work (Catppuccin Mocha and Latte).
  "theme": "auto",
  // Tab chips show the checked-out branch after the name (default false: it makes tabs long).
  "tabs": { "showBranch": false },
  // Folders the repository picker scans for repositories (default: Projects, src, code, dev, ... below the home folder).
  "scanRoots": ["~/Projects", "/srv/git"],
  // Ends of the branch labels in the graph: rounded (default) or angled Powerline caps, or none for a plain box.
  // Needs a Nerd Font or Powerline-patched font; a change in the Settings screen applies at once.
  "labelCaps": "rounded",
  // Hints that teach keys. whichKey: on (default) or off, the popup after a pending chord. statusBar: full (default),
  // minimal (only ? help, : commands, q quit) or off. The keys work either way; a change in Settings applies at once.
  "hints": { "whichKey": "on", "statusBar": "full" },
  // Copy a mouse selection (drag, double or triple click) to the clipboard when you let go (default true).
  "copyOnSelect": true,
  // Live file watching instead of polling (default false: each watcher takes a scarce inotify instance; read at startup).
  "watchFiles": false,
  "keys": [
    // Bind a free key (mode defaults to Normal, view to Global).
    { "view": "Graph", "key": "J", "action": "list.down" },
    // Replace what a key does.
    { "key": "q", "action": "palette.open" },
    // Unbind: the action "none".
    { "view": "Lists", "key": "gg", "action": "none" }
  ]
}
```

- A step-by-step guide with examples is [customising-keys.md](../customising-keys.md); this section is the reference.
- Entry fields: `mode` (`Normal`, `Visual`, `Insert`, `Command`, `Confirm`), `view` (`Global`, `Lists`, `Graph`, ... as in the [keymap reference](../keymap.md); a view's keys also apply to the views that inherit from it), `key` (notation of the [keymap reference](../keymap.md)), `action` (id from the palette/help, or `none`).
  One entry binds one mode and view; bind a key in Normal and Visual with two entries.
- An entry on a key that already has a binding in that mode and view replaces it; `none` removes it (and uncovers a binding of the parent view, if any). Two entries for the same mode, view and key: the first applies, the second is reported.
- Each entry is checked alone.
  A bad one (unknown mode, view or action, key that does not parse, nothing to unbind, a field of the wrong type such as `"key": 5`, an entry that is not an object) is skipped and reported; the others apply.
  A file that is not valid JSON, is unreadable or is over 1 MB is ignored as a whole.
  The app always starts: the defaults fill in whatever could not be used.
- Conflicts: the engine always waits for a longer chord (no timeout), so a key cannot be both complete and the start of a longer chord (`g` next to `gg`, `gt`).
  An entry that would create that, in its view or any view that inherits from it, is refused with a message naming both keys.
  Unbind the other key first (earlier entry) if you want the shorter one.
- Config can rebind guard rails too (a key for `confirm.yes` in Confirm mode, `Esc`, the quit key); it is the user's file and an entry is accepted as long as no conflict arises.
- Options (#453), read by `OptionsConfigurator` (Core, pure; the folder check is injected).
  `tabs.showBranch`: a boolean; a `tabs` that is not an object or a non-boolean value is reported and false applies.
  `scanRoots`: a list of absolute paths, a leading `~` stands for the home folder; it replaces the conventional folders, an empty list turns discovery off, and the picker scans these folders only (depth and count limits unchanged).
  An entry that is not a string, is relative (never resolved against the process working directory) or is not an existing folder is reported by index (`scanRoots[2]`) and skipped; if no entry of a non-empty list is usable, or the value is not a list, the conventional folders apply.
  `labelCaps`: `rounded` (default), `angled` or `none` (any case); another value is reported and `rounded` applies. When the file sets none, the default is `rounded` everywhere, the classic Windows console included (it was `none` there because its font has no Powerline glyphs and the caps drew as empty boxes, #574; the owner chose caps on by default, 2026-11-03, so such a console sets `labelCaps` to `none`); an explicit value always wins. In the classic console only, the 16-colour dark scheme also uses bright cyan instead of bright blue for the accent and the chips, since dark blue is hard to read on its black, plain grey for secondary (muted) text and always white base text so muted grey stays apart (its dark grey is too dim on black), and a blue selection bar instead of bright black, with white text on the focused selected row (grey and magenta on its grey were barely readable, #915). Rounded draws U+E0B6 before and U+E0B4 after each branch label box, angled U+E0B2 and U+E0B0, each one cell in the box colour (needs a Nerd Font or Powerline-patched font); with caps the box has no padding of its own. Only the Unicode tier with colours takes them; the ASCII tier, a no-colour terminal and `none` keep the plain box. A change in the settings screen redraws every tab's graph at once.
  `hints.whichKey` and `hints.statusBar` (#837): `on`/`off` and `full`/`minimal`/`off` (any case); a `hints` that is not an object or another value is reported and the default (`on`, `full`) applies. Off hides the popup only (the pending prefix still shows in the status bar); `minimal` keeps `? help  : commands  q quit`, `off` shows no hints; `?` and `:` always work. Modal hints (confirm answers, editors, the repeat mode) are not affected. With the popup off, a group hint such as `<space>v view options` still shows in `full` but opens no popup (the keys below it work); `minimal` and `off` also drop the conflict-editor hint during a stopped operation. Applied at once from the settings screen (Keys category).
  `copyOnSelect` (#1188): a boolean, true by default; another value is reported and true applies. Applied at once from the settings screen (Keys category). See "Drag to select" in `ux-mouse.md`.
  `watchFiles` (#543): a boolean, false by default (tabs poll, see `git-layer.md`); another value is reported and false applies; it has no row in the settings screen and takes effect after a restart.
  Options are read at startup; the settings screen applies a change at once (and reads the file again when it opens and when the editor row returns).
- Problems appear as warnings at startup (the first in the status bar with a count of the others; `<space>m` keeps the first five).
  The text span of any value is found by `ConfigKeyLocator.Locate` (found, absent, empty, invalid with line, parent not an object; the last duplicate wins), which `ConfigTextEditor` and `ConfigWriter` (#522) use to change one key and keep comments and formatting.

