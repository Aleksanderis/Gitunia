# Terminal.Gui spike (reference only)

Preserved copy of the M0 spike #4 "heavy screen", later polished to match the custom demo's look (header bar, tab row, dimmed hashes, status hints) (Terminal.Gui v2 2.5.0): titled window, tab row, refs sidebar, commit graph with lanes, async `git show --stat` details pane, status bar, vim-style keys. It is a reference for building the M1 UI, not production code: it is not referenced by the app and is not maintained. Measurements and pitfalls (iteration rate, streaming list source, app-level key handling) are in `docs/decisions.md`.

Run: `dotnet run --project samples/Gitunia.TerminalGuiSpike -- <repo> [--ips 100]`.
