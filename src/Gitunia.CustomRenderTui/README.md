# Gitunia.CustomRenderTui

A small, self-contained terminal renderer: cell buffer with diff-based flush, VT input parser and a raw-mode terminal for Linux, macOS and Windows.

## Status: parked, NOT actively maintained

Gitunia's renderer is **Terminal.Gui v2** (see `docs/decisions.md`). This project is kept only as a **future option**:

- the app does not reference it;
- Terminal.Gui is the only active renderer, and features are **not** mirrored here;
- it is kept building and tested in CI (`dotnet build` / `dotnet test` cover it); a Windows ConPTY smoke test runs manually or when its own files change;
- it may fall behind the main code. That is accepted.

If you revive it, start with the "Missing" list below and the numbers in this file.

## Contents

| Type | Purpose |
|---|---|
| `CellBuffer` | Cell grid (char, style, 256-colour foreground); `Flush()` returns only the changed cells as one UTF-8 escape-sequence write |
| `InputParser` | Incremental parser: UTF-8 text, Ctrl combinations, CSI/SS3 keys, SGR mouse wheel; sequences split across reads are completed by the next read |
| `RawTerminal` | Raw mode, alt screen, mouse reporting, size, input reads and resize notifications. Unix: termios, `ioctl`, SIGWINCH. Windows: console modes (VT input/output), `ReadConsoleInputW`, UTF-8 code pages |

`RawTerminal` never touches .NET's `Console` input or size APIs: they make .NET manage the terminal itself (echo, line buffering) and break raw mode.

**Exception to a repo rule:** `CLAUDE.md` says platform-specific code lives only in `Gitunia.Platform`. This project owns its OS-specific console code on purpose so it stays self-contained and removable. Nothing in the app depends on it.

## Demo

`samples/Gitunia.CustomRenderTui.Demo` is an M1-like screen over a streamed `git log`: title and tab bar, refs sidebar, coloured commit graph lanes, async commit details (`git show --stat`, cancelled when the selection changes) and a status line.

```
dotnet publish samples/Gitunia.CustomRenderTui.Demo -c Release -o out
out/gitunia-custom-demo <repo-path>
```

Keys: `Tab` switch pane, `j`/`k`, `gg`/`G`, `Ctrl-d`/`Ctrl-u`, `/` search, `n`/`N`, `q`. For a 100k-commit repo use `scripts/gen-big-repo.sh`, then `scripts/measure.sh out/gitunia-custom-demo <repo>`.

## Windows smoke test

`tools/ConPtyHarness` runs an exe under a Windows pseudoconsole (ConPTY), sends keys and a resize and asserts on the VT output. The workflow `.github/workflows/custom-render-windows.yml` publishes the demo for win-x64 and runs it (manual only: Actions tab > Run workflow). Locally on Windows:

```
dotnet build tools/ConPtyHarness -c Release -o harness
./scripts/make-fixture-repo.ps1 -Path $env:TEMP/fixture
dotnet harness/ConPtyHarness.dll out/gitunia-custom-demo.exe $env:TEMP/fixture --strict
```

## Spike measurements (Linux x64, 100k-commit repo, `scripts/measure.sh` medians, include 1-3 ms tmux polling)

| | List only | Heavy screen (this demo), 120x40 |
|---|---|---|
| Binary | 2.56 MB | 2.6 MB |
| Cold start | ~28 ms | ~24-34 ms |
| `j` to frame | ~4.8 ms | ~4-7 ms |
| RSS | ~33 MB | ~35-45 MB |

For comparison Terminal.Gui v2 measured ~97-107 ms cold start, ~34 ms key latency (~13 ms with the iteration rate raised to 100) and ~50-71 MB RSS. Windows (ConPTY on `windows-latest`): AOT publish clean, 10/10 smoke checks, ~84-118 ms to first frame including CI and ConPTY overhead.

## Missing

- **Windows hardening (about 1-2 days):** restoring console modes and code pages was only code-reviewed; legacy conhost without VT input fails with an error; arrow keys, Home/End, Alt combinations and mouse are untested on Windows; no performance numbers; macOS untested.
- **Lone ESC ambiguity:** a lone `ESC` is reported as Escape immediately, with no timeout to tell it from the start of a sequence split across reads.
- **Wide characters:** cell width is assumed to be 1 (CJK, emoji and combining marks break alignment).
- **Colour fallback:** only 256-colour foreground; no 16-colour or truecolor path, no background colours.
- **Widgets:** no text input with a cursor, dialogs, menus or command palette (the demo's search is a crude one-line prompt), no mouse hit-testing beyond the wheel.
- **Clipboard (OSC 52)** and suspend/resume for external tools.
