# Release checklist

*Internal note for maintainers. How the pieces fit: `docs/architecture.md`, "Releases"; why: `docs/internal/decisions.md`.*

Tags are three-part from the next release (`v0.10.0`, then `v0.10.1`, ...). Earlier releases are two-part (`v0.9`). Both forms work everywhere: `release.yml` accepts `v0.8`, `v1.2.3` and `v1.2.3-rc.1`; `scripts/homebrew-formula.sh` and `packaging/aur/update.sh` take `x.y` and `x.y.z` (#1278), and the install scripts take the tag as given (strip the `v`, compare as text, pick assets by suffix); `gitunia update` reads `v0.9` as `0.9.0`, so `v0.9 < v0.10.0` and `v0.9 == v0.9.0` (`ReleaseVersion`, #1299).

## 1. Before tagging

- `main` is the commit to release, pushed, and its CI is green. A green full `ci.yml` run (`workflow_dispatch`) for that commit is reused by the release; without one the release dispatches CI itself and waits.
- Optional dry run (builds and packs every leg, makes no release and no tag; archives stay as workflow artifacts):
  `gh workflow run release.yml --ref <branch> -f tag=v0.0.0-dryrun`
- Check the winget state: the `WINGET_SUBMIT` repository variable (only `true` submits) and any open `Gitunia.Gitunia` PR in winget-pkgs (an open one makes the run skip).

## 2. Tag

`git tag v0.10.0` and `git push origin v0.10.0` (only with the owner's say-so, like any push). The tag push starts `release.yml`:

1. `check`: validates the tag shape, reuses or dispatches a green `ci.yml` run for the commit.
2. `build-x64` (linux-x64, win-x64) and `build-extra` (osx-arm64, linux-arm64, win-arm64): AOT publish with `-p:Version`, archive smoke test (`--version`), Linux `.deb`/`.rpm`/Arch install tests in clean containers. A red extra leg does not block the draft.
3. `release`: makes a **draft** release with generated notes and writes an overview of the downstream steps to its step summary.

To re-run a real release by hand (the tag must exist; dispatch on the tag): `gh workflow run release.yml --ref v0.10.0 -f tag=v0.10.0 -f dry_run=false`.

## 3. Review the draft

- Asset list: archive and `.sha256` for every platform you expect (arm64 and osx files are missing when an extra leg was red), `.deb`, `.rpm`, Arch package.
- A draft without the `osx-arm64` or `linux-arm64` archives makes Homebrew (and probably AUR) fail after publishing: fix the red leg and re-run `release.yml` first, or expect to catch those steps up by hand.
- Read and edit the generated notes; read the `release` job's step summary for what the downstream workflows will do.
- Tick "pre-release" only for a real pre-release: all three downstream workflows skip those.

## 4. Publish

Publishing the draft triggers three workflows (`release: published`):

- `winget.yml`: submits with wingetcreate only when `WINGET_SUBMIT` is `true` and `WINGET_TOKEN` is set, and not while an earlier `Gitunia.Gitunia` PR of the owner is open in winget-pkgs. Otherwise it ends green with a notice and does nothing.
- `homebrew.yml`: commits `Formula/gitunia.rb` to `Aleksanderis/homebrew-tap` using `HOMEBREW_TAP_TOKEN` (no switch; green notice without the token, red if the tap cannot be cloned).
- `aur.yml`: pushes `gitunia-bin` using `AUR_SSH_KEY` (no switch; green notice while the key is missing).

Every run that does nothing writes the reason to its job summary. After publishing, read the three runs (`gh run list --workflow winget.yml -L 1`, same for `homebrew.yml` and `aur.yml`).

## 5. Catch up a downstream step by hand

Manual runs work for a published release and skip the pre-release check, but still need their secret (`WINGET_TOKEN`, `HOMEBREW_TAP_TOKEN`, `AUR_SSH_KEY`).

- Homebrew: `gh workflow run homebrew.yml -f version=0.10.0` (version without the `v`: `0.9` or `0.10.0`).
- AUR: `gh workflow run aur.yml -f version=0.10.0`.
- winget: `gh workflow run winget.yml -f tag=v0.10.0` (with the `v`). Add `-f force=true` to submit although an earlier PR is still open. A manual run ignores `WINGET_SUBMIT`. A release skipped because of an open PR is not retried, so dispatch it once that PR is merged or closed.

Agents never dispatch the winget workflow and never write to winget-pkgs (`docs/internal/decisions.md`).

## 6. Afterwards

- `gitunia --version` from the released archive, and `gitunia update --check` on an older install, show the new release.
- Anything that went wrong becomes an issue, not a note.
