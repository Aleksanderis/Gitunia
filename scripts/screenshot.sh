#!/usr/bin/env bash
# Screenshots of the real UI for pull requests (Linux/macOS; bash, tmux, rsvg-convert, dotnet, git; `gh` for publish/clean).
# Pipeline: the real binary runs in a private tmux server on a throwaway repo, `tmux capture-pane -p -e`
# gives the ANSI frame, tools/AnsiToSvg turns it into an SVG and rsvg-convert into a PNG. Headless, no network.
# Windows is unsupported here (no tmux): the script says so and exits 2; use scripts/screenshot.ps1 (psmux, resvg). See the screenshots skill (.claude/skills/screenshots/SKILL.md).
#
# Usage:
#   scripts/screenshot.sh render  --binary <gitunia> [--out <dir>] [--scenarios default,diff,split,narrow,light,files,files-filter,file-content,palette,help,help-lists,whichkey,menu,menu-sidebar,sidebar-ref,prompt,prompt-narrow,jump,jump-narrow,jump-diff,jump-working,working,working-narrow,working-diff,working-diff-narrow,working-zoom,zoom-both,view-menu,working-view-menu,list-view-menu,files-view-menu,working-list-menu,working-stage,working-hunk,working-lines,working-commit,working-hook,working-discard,working-resolve,status-fresh,status-settled,settings,settings-choice,settings-narrow,settings-keys,settings-keys-entry,theme-preview,theme-kept,theme-tokyo,theme-latte,rebase-editor,rebase-reword,rebase-confirm,rebase-narrow,conflict-editor,conflict-editor-narrow,conflict-editor-edit,conflict-editor-base,stash,whitespace] [--repo <dir>]
#   scripts/screenshot.sh publish --pr <number> [--out <dir>]     push the PNGs of `render` to the screenshots repository,
#                                                                  print markdown for a PR comment
#   scripts/screenshot.sh clean                                    remove images of merged/closed PRs from the screenshots repository
# Images live in a separate repository, <owner>/<repo>-screenshots (override: GITUNIA_SCREENSHOTS_REPO=owner/name), branch main,
# so the code repository never carries them. It is private: links are github.com/<screenshots repo>/raw/<commit>/... (render for
# logged-in viewers; raw.githubusercontent.com needs a token). They are pinned to the screenshots commit, so later pushes never break
# them; `clean` deletes the files of merged PRs from the branch tip, but history keeps them (the owner may reset that repository).
# Scenarios: theme-preview (the theme list with the light theme previewed) and theme-kept (the same after Enter: the theme is saved and stays), theme-tokyo and theme-latte (the same with the built-in tokyo-night and catppuccin-latte previewed); settings, settings-choice (an open choice) and settings-narrow (64x24, collapsed layout) show the settings screen (<space>,); settings-keys (the Keys category: the key reference) and settings-keys-entry (Enter on a key: its config entry in the status bar); default (120x36), diff (diff pane open), split (170 columns, side-by-side diff), narrow (64x30), light (light terminal background), files (gf Enter Enter: the Files tab with the first directory opened), files-filter (gf Enter / then mul: the tree filter) and file-content (gf Enter Enter j Enter: the content of the first file of the first directory in the diff pane's place),
# prompt and prompt-narrow (<space>bc then feat: the new-branch prompt popup), jump and jump-narrow (<space>j then c: the quick jump labels in the graph), jump-diff (the same in the file diff, typing e) and jump-working (the same in the Unstaged list of the working-tree view), working, working-narrow, working-stage (after <space>a twice), working-hunk (the hunk of a file staged in its diff) and working-lines (a Visual range selected in the diff), working-zoom (the diff zoomed with <space>z), zoom-both (the history diff zoomed with <space>z: Changes and diff side by side), view-menu and working-view-menu (<space>vv: the view ▾ dropdown of the history and the working-tree diff title), list-view-menu, files-view-menu and working-list-menu (<space>vv in the Changes list, the Files tree and the Unstaged list: their view ▾ dropdowns), working-diff and working-diff-narrow (<space>s, then the diff of a file with Enter: the working-tree view of a copy of the throwaway repository with a conflict, staged and unstaged changes and untracked files; not for --repo).
# working-commit (staged files, a message typed in the box with SignOff on) and working-hook (the same, committed against a failing pre-commit hook: the output overlay); not for --repo.
# working-discard (<space>d on README.md, the second file of the Unstaged list; the first is a conflict, which is refused: the Confirm dialog); not for --repo.
# rebase-editor (G <space>gi, then squash, drop and so on: the interactive rebase todo editor on a linear branch of a copy of the throwaway repository), rebase-reword (r: the message editor of a reword), rebase-confirm (Enter: the Confirm over the editor) and rebase-narrow (64x24); not for --repo.
# stash (C-w h G Enter: a stash shown in the detail pane, with a tracked and an untracked file; a copy of the throwaway repository with a stash; not for --repo).
# whitespace (Enter Enter <space>vc: the diff of the latest commit with whitespace marks; meant for --repo with tabs and trailing spaces in it).
# conflict-editor and conflict-editor-narrow (<space>s, Enter on a merge conflict with two hunks, then o and n: the conflict editor after one pick; a copy of the throwaway repository stopped on a merge; not for --repo).
# conflict-editor-edit (o e then typed text: the result as a text edit) and conflict-editor-base (100 columns, under the three-pane width; o B: the base block below the two sides); not for --repo.
# working-resolve (<space>a on the conflicted first file: the mark-resolved Confirm dialog); not for --repo.
# Without --repo a deterministic throwaway repo is generated under a temp dir (never a real repo).
# Fonts differ from the owner's terminal, so glyph widths stay a manual check (#196).
set -euo pipefail

command_name="${1:-}"
[ $# -gt 0 ] && shift

case "$(uname -s)" in
  Linux*|Darwin*) ;;
  *) echo "screenshot.sh is unsupported on this OS (needs tmux); use the snapshot tests or --print-frame instead" >&2; exit 2 ;;
esac

usage() { sed -n '2,12p' "$0" >&2; exit 2; }
need() { # need TOOL HINT
  command -v "$1" >/dev/null 2>&1 || { echo "missing tool '$1': $2" >&2; exit 3; }
}

script_dir="$(cd "$(dirname "$0")" && pwd)"
repo_root="$(cd "$script_dir/.." && pwd)"
cache_dir="${XDG_CACHE_HOME:-$HOME/.cache}/gitunia/screenshot"
out_dir="$cache_dir/out"
binary="" scenarios="default,diff,narrow,light" source_repo="" pull_request=""
while [ $# -gt 0 ]; do
  case "$1" in
    --binary|--out|--scenarios|--repo|--pr)
      [ $# -ge 2 ] || { echo "$1 needs a value" >&2; usage; }
      case "$1" in
        --binary) binary="$2" ;;
        --out) out_dir="$2" ;;
        --scenarios) scenarios="$2" ;;
        --repo) source_repo="$2" ;;
        --pr) pull_request="$2" ;;
      esac
      shift 2 ;;
    *) echo "unknown option $1" >&2; usage ;;
  esac
done

branch_name="main"
work="" socket=""

make_throwaway_repo() { # make_throwaway_repo DIR: small deterministic repo with branches, a merge, tags and real diffs
  local dir="$1" day=0
  ( # a subshell keeps the GIT_* variables out of the tmux server and the app under test
  export GIT_CONFIG_GLOBAL=/dev/null GIT_CONFIG_SYSTEM=/dev/null
  export GIT_AUTHOR_NAME="Ada Lovelace" GIT_AUTHOR_EMAIL=ada@example.com GIT_COMMITTER_NAME="Ada Lovelace" GIT_COMMITTER_EMAIL=ada@example.com
  git init -q -b main "$dir"
  commit() { # commit MESSAGE
    day=$((day + 1))
    export GIT_AUTHOR_DATE="2026-03-$(printf '%02d' "$day")T12:00:00Z" GIT_COMMITTER_DATE="2026-03-$(printf '%02d' "$day")T12:00:00Z"
    git -C "$dir" add -A
    git -C "$dir" commit -q -m "$1"
  }
  printf '# Demo\n\nA small demo project.\n' >"$dir/README.md"
  mkdir -p "$dir/src"
  printf 'int Add(int left, int right) => left + right;\n' >"$dir/src/math.txt"
  commit "Initial commit"
  printf 'int Subtract(int left, int right) => left - right;\n' >>"$dir/src/math.txt"
  commit "Add subtract"
  git -C "$dir" tag v0.1
  git -C "$dir" switch -q -c feature/multiply
  printf 'int Multiply(int left, int right) => left * right;\n' >"$dir/src/multiply.txt"
  commit "Add multiply"
  printf 'Multiply is exact for small values.\n' >"$dir/src/multiply.md"
  commit "Document multiply"
  git -C "$dir" switch -q main
  printf 'int Divide(int left, int right) => left / right;\n' >>"$dir/src/math.txt"
  printf 'Division truncates.\n' >>"$dir/README.md"
  commit "Add divide with a very long subject that has to be truncated when the terminal is narrow"
  day=$((day + 1))
  export GIT_AUTHOR_DATE="2026-03-$(printf '%02d' "$day")T12:00:00Z" GIT_COMMITTER_DATE="2026-03-$(printf '%02d' "$day")T12:00:00Z"
  git -C "$dir" merge -q --no-ff -m "Merge feature/multiply" feature/multiply
  printf 'int Modulo(int left, int right) => left %% right;\n' >>"$dir/src/math.txt"
  commit "Add modulo"
  git -C "$dir" switch -q -c fix/overflow
  git -C "$dir" switch -q main
  )
}

make_stash_repo() { # make_stash_repo SOURCE DIR: a copy of the throwaway repository with a stash holding a tracked and an untracked file
  local source="$1" dir="$2"
  cp -r "$source" "$dir"
  ( # a subshell keeps the GIT_* variables out of the tmux server and the app under test
  export GIT_CONFIG_GLOBAL=/dev/null GIT_CONFIG_SYSTEM=/dev/null
  export GIT_AUTHOR_NAME="Ada Lovelace" GIT_AUTHOR_EMAIL=ada@example.com GIT_COMMITTER_NAME="Ada Lovelace" GIT_COMMITTER_EMAIL=ada@example.com
  export GIT_AUTHOR_DATE="2026-04-01T12:00:00Z" GIT_COMMITTER_DATE="2026-04-01T12:00:00Z"
  printf 'Division truncates toward zero.\n' >>"$dir/README.md"
  printf 'todo\n' >"$dir/notes.txt"
  git -C "$dir" stash push -q --include-untracked -m "WIP notes"
  )
}

make_dirty_repo() { # make_dirty_repo SOURCE DIR: a copy of the throwaway repository with a merge conflict and every kind of change
  local source="$1" dir="$2"
  cp -r "$source" "$dir"
  ( # a subshell keeps the GIT_* variables out of the tmux server and the app under test
  export GIT_CONFIG_GLOBAL=/dev/null GIT_CONFIG_SYSTEM=/dev/null
  export GIT_AUTHOR_NAME="Ada Lovelace" GIT_AUTHOR_EMAIL=ada@example.com GIT_COMMITTER_NAME="Ada Lovelace" GIT_COMMITTER_EMAIL=ada@example.com
  export GIT_AUTHOR_DATE="2026-04-01T12:00:00Z" GIT_COMMITTER_DATE="2026-04-01T12:00:00Z"
  git -C "$dir" switch -q -c side fix/overflow
  printf 'int Add(long left, long right) => left + right;\n' >"$dir/src/math.txt"
  git -C "$dir" commit -q -am "Widen Add"
  git -C "$dir" switch -q main
  printf 'int Add(int left, int right) => checked(left + right);\n' >"$dir/src/math.txt"
  git -C "$dir" commit -q -am "Check Add for overflow"
  git -C "$dir" merge -q side >/dev/null 2>&1 || true
  git -C "$dir" mv src/multiply.md src/multiply-notes.md
  printf 'Division truncates toward zero.\n' >>"$dir/README.md"
  git -C "$dir" add README.md
  printf 'int Power(int value, int exponent) => 1;\n' >"$dir/src/power.txt"
  git -C "$dir" add src/power.txt
  printf 'Edited again.\n' >>"$dir/README.md"
  rm "$dir/src/multiply.txt"
  mkdir -p "$dir/notes"
  printf 'todo\n' >"$dir/notes/todo.txt"
  printf 'x\n' >"$dir/Ünïcode name.txt"
  )
}

make_conflict_repo() { # make_conflict_repo SOURCE DIR: a copy of the throwaway repository stopped on a merge conflict with two hunks in one file
  local source="$1" dir="$2" lines
  cp -r "$source" "$dir"
  lines="$(printf 'line %s\n' 1 2 3 4 5 6 7 8 9 10 11 12)"
  ( # a subshell keeps the GIT_* variables out of the tmux server and the app under test
  export GIT_CONFIG_GLOBAL=/dev/null GIT_CONFIG_SYSTEM=/dev/null
  export GIT_AUTHOR_NAME="Ada Lovelace" GIT_AUTHOR_EMAIL=ada@example.com GIT_COMMITTER_NAME="Ada Lovelace" GIT_COMMITTER_EMAIL=ada@example.com
  export GIT_AUTHOR_DATE="2026-04-01T12:00:00Z" GIT_COMMITTER_DATE="2026-04-01T12:00:00Z"
  printf '%s\n' "$lines" >"$dir/src/notes.txt"
  git -C "$dir" add src/notes.txt
  git -C "$dir" commit -q -m "Add notes"
  git -C "$dir" switch -q -c side
  sed -i.bak -e 's/^line 3$/line 3, as the side branch wrote it/' -e 's/^line 9$/line 9 (side)/' "$dir/src/notes.txt"
  rm -f "$dir/src/notes.txt.bak"
  git -C "$dir" commit -q -am "Reword notes on the side"
  git -C "$dir" switch -q main
  sed -i.bak -e 's/^line 3$/line 3 reworded on main/' -e 's/^line 9$/line 9 (main)\nand a new line after it/' "$dir/src/notes.txt"
  rm -f "$dir/src/notes.txt.bak"
  git -C "$dir" commit -q -am "Reword notes on main"
  git -C "$dir" merge -q side >/dev/null 2>&1 || true
  )
}

make_commit_repo() { # make_commit_repo SOURCE DIR HOOK: a copy with staged, unstaged and untracked changes (no merge); HOOK=hook adds a failing pre-commit hook
  local source="$1" dir="$2" hook="$3"
  cp -r "$source" "$dir"
  ( # a subshell keeps the GIT_* variables out of the tmux server and the app under test
  export GIT_CONFIG_GLOBAL=/dev/null GIT_CONFIG_SYSTEM=/dev/null
  printf 'Division truncates toward zero.\n' >>"$dir/README.md"
  printf 'int Power(int value, int exponent) => 1; // TODO\n' >"$dir/src/power.txt"
  git -C "$dir" add README.md src/power.txt
  printf 'int Modulo(int left, int right) => left %% right; // edited\n' >>"$dir/src/math.txt"
  printf 'todo\n' >"$dir/notes.txt"
  )
  if [ "$hook" = "hook" ]; then
    printf '#!/bin/sh\necho "pre-commit: found TODO in src/power.txt" >&2\necho "pre-commit: remove it or commit with No-Verify" >&2\nexit 1\n' >"$dir/.git/hooks/pre-commit"
    chmod +x "$dir/.git/hooks/pre-commit"
  fi
}

make_rebase_repo() { # make_rebase_repo SOURCE DIR: a copy of the throwaway repository with a linear branch of five commits on top of v0.1
  local source="$1" dir="$2" index=0
  cp -r "$source" "$dir"
  ( # a subshell keeps the GIT_* variables out of the tmux server and the app under test
  export GIT_CONFIG_GLOBAL=/dev/null GIT_CONFIG_SYSTEM=/dev/null
  export GIT_AUTHOR_NAME="Ada Lovelace" GIT_AUTHOR_EMAIL=ada@example.com GIT_COMMITTER_NAME="Ada Lovelace" GIT_COMMITTER_EMAIL=ada@example.com
  git -C "$dir" switch -q -c cleanup v0.1
  local subject
  for subject in "Rename Add to Sum" "Fix typo in README" "Add Sum tests" "Remove dead code" "Update the changelog"; do
    index=$((index + 1))
    export GIT_AUTHOR_DATE="2026-04-0${index}T12:00:00Z" GIT_COMMITTER_DATE="2026-04-0${index}T12:00:00Z"
    printf 'change %s\n' "$index" >"$dir/cleanup-$index.txt"
    git -C "$dir" add -A
    git -C "$dir" commit -q -m "$subject" -m "Longer explanation of: $subject."
  done
  )
}

type_message() { # types a subject, a blank line, a body line and turns SignOff on (Alt-s) in the open message box
  tmux_cmd send-keys -t shot -l "Drop stale cache entries on refresh"
  tmux_cmd send-keys -t shot Enter Enter
  tmux_cmd send-keys -t shot -l "Entries were never dropped when the status changed."
  tmux_cmd send-keys -t shot M-s
}

build_converter() { # prints the converter dll path
  local dll="$cache_dir/AnsiToSvg/AnsiToSvg.dll"
  if [ ! -f "$dll" ] || [ "$repo_root/tools/AnsiToSvg/Program.cs" -nt "$dll" ]; then
    echo "building tools/AnsiToSvg ..." >&2
    dotnet build "$repo_root/tools/AnsiToSvg" -c Release -o "$cache_dir/AnsiToSvg" -v quiet -nologo >&2 || { echo "converter build failed" >&2; exit 3; }
  fi
  echo "$dll"
}

cleanup() {
  [ -n "${socket:-}" ] && tmux -L "$socket" kill-server 2>/dev/null
  [ -n "${work:-}" ] && rm -rf "$work"
  return 0
}
trap cleanup EXIT

render() {
  need tmux "install tmux"
  need rsvg-convert "install librsvg (rsvg-convert)"
  need dotnet "install the .NET SDK"
  need git "install git"
  [ -n "$binary" ] || { echo "--binary <path to gitunia> is required (build: dotnet build src/Gitunia.Tui -c Release -o <dir>)" >&2; exit 2; }
  [ -x "$binary" ] || { echo "binary not found or not executable: $binary" >&2; exit 2; }
  binary="$(cd "$(dirname "$binary")" && pwd)/$(basename "$binary")"
  work="$(mktemp -d)"
  socket="gtshot-$$"
  mkdir -p "$out_dir" "$cache_dir"
  rm -f "$out_dir"/*.png # stale images of earlier runs must never be published
  local converter repo
  build_converter >/dev/null
  converter="$cache_dir/AnsiToSvg/AnsiToSvg.dll"
  repo="$source_repo"
  tmux_cmd() { tmux -L "$socket" -f /dev/null "$@"; }
  if [ -z "$repo" ]; then repo="$work/repo"; make_throwaway_repo "$repo"; fi
  mkdir -p "$work/state" "$work/config"
  tmux_cmd start-server \; set -g exit-empty off \; set -g remain-on-exit on \; set -g escape-time 0 \; set -ga terminal-features ',*:RGB' \; set -ga terminal-overrides ',*:Tc'

  local scenario
  for scenario in ${scenarios//,/ }; do
    local columns=120 rows=36 theme=dark keys="" keys_after=""
    case "$scenario" in
      default) ;;
      diff) keys="Enter Enter" ;;
      zoom-both) keys="Enter Enter Space z" ;;
      view-menu) keys="Enter Enter Space v v" ;;
      working-view-menu) keys="Space s j Enter j Space v v" ;;
      list-view-menu) keys="Enter Space v v" ;;
      files-view-menu) keys="g f Enter Space v v" ;;
      working-list-menu) keys="Space s Space v v" ;;
      split) columns=170 keys="Enter Enter S" ;;
      whitespace) keys="Enter Enter Space v c" ;;
      narrow) columns=64 rows=30 ;;
      light) theme=light ;;
      files) keys="g f Enter Enter" ;;
      files-filter) keys="g f Enter / m u l" ;;
      file-content) keys="g f Enter Enter j Enter" ;;
      palette) rows=20 keys=": C-j C-j" ;;
      whichkey) keys="Space" ;;
      help) rows=30 keys="?" ;;
      help-lists) rows=30 keys="? Tab Tab" ;;
      menu) keys="j j Space ." ;;
      menu-sidebar) keys="C-w h j j Space ." ;;
      sidebar-ref) keys="C-w h j j j" ;;
      stash) keys="C-w h G Enter" ;;
      prompt) keys="Space b c f e a t" ;;
      prompt-narrow) columns=64 rows=30 keys="Space b c f e a t" ;;
      jump) keys="Space j c" ;;
      jump-narrow) columns=64 rows=30 keys="Space j c" ;;
      jump-diff) keys="Enter Enter Space j e" ;;
      jump-working) keys="Space s Space j e" ;;
      working) keys="Space s" ;;
      working-narrow) columns=64 rows=30 keys="Space s" ;;
      working-diff) keys="Space s j Enter j" ;;
      working-diff-narrow) columns=64 rows=30 keys="Space s j Enter" ;;
      working-zoom) keys="Space s j Enter Space z" ;;
      working-stage) keys="Space s Space a Space a" ;;
      working-hunk) keys="Space s j Enter" keys_after="Space a" ;;
      working-lines) keys="Space s j Enter" keys_after="j j j j j v" ;;
      working-discard) keys="Space s j" keys_after="Space d" ;;
      working-resolve) keys="Space s" keys_after="Space a" ;;
      settings) keys="Space ," ;;
      settings-choice) keys="Space , Enter Enter j" ;;
      settings-narrow) columns=64 rows=24 keys="Space ," ;;
      settings-keys) keys="Space , j Enter j j j" ;;
      settings-keys-entry) keys="Space , j Enter j j j Enter" ;;
      theme-preview) keys="Space , Enter Enter j j" ;;
      theme-kept) keys="Space , Enter Enter j j Enter" ;;
      theme-tokyo) keys="Space ," keys_after="Enter Enter j j j j j j j" ;;
      theme-latte) keys="Space ," keys_after="Enter Enter j j j j j j" ;;
      status-fresh) keys="Space F" ;;
      status-settled) keys="Space F" keys_after="Space F" ;;
      working-commit | working-hook) keys="Space s Space a Space a Space c" ;;
      conflict-editor) keys="Space s Enter" keys_after="o n" ;;
      conflict-editor-narrow) columns=64 rows=30 keys="Space s Enter" keys_after="o n" ;;
      conflict-editor-edit) keys="Space s Enter" keys_after="o e t y p e d" ;;
      conflict-editor-base) columns=100 rows=36 keys="Space s Enter" keys_after="o B" ;;
      rebase-editor) keys="G Space g i" keys_after="j s j j d" ;;
      rebase-reword) keys="G Space g i" keys_after="j r" ;;
      rebase-confirm) keys="G Space g i" keys_after="j s j j d Enter" ;;
      rebase-narrow) columns=64 rows=24 keys="G Space g i" keys_after="j s j j d" ;;
      *) echo "unknown scenario '$scenario'" >&2; exit 2 ;;
    esac
    local scenario_repo="$repo"
    if [[ "$scenario" == working-commit && -z "$source_repo" ]]; then
      scenario_repo="$work/repo-commit"
      [ -d "$scenario_repo" ] || make_commit_repo "$repo" "$scenario_repo" plain
    elif [[ "$scenario" == working-hook && -z "$source_repo" ]]; then
      scenario_repo="$work/repo-hook"
      [ -d "$scenario_repo" ] || make_commit_repo "$repo" "$scenario_repo" hook
    elif [[ "$scenario" == conflict-editor* && -z "$source_repo" ]]; then
      scenario_repo="$work/repo-conflict"
      [ -d "$scenario_repo" ] || make_conflict_repo "$repo" "$scenario_repo"
    elif [[ "$scenario" == rebase-* && -z "$source_repo" ]]; then
      scenario_repo="$work/repo-rebase"
      [ -d "$scenario_repo" ] || make_rebase_repo "$repo" "$scenario_repo"
    elif [[ "$scenario" == stash && -z "$source_repo" ]]; then
      scenario_repo="$work/repo-stash"
      [ -d "$scenario_repo" ] || make_stash_repo "$repo" "$scenario_repo"
    elif [[ "$scenario" == working* && -z "$source_repo" ]]; then
      scenario_repo="$work/repo-dirty"
      [ -d "$scenario_repo" ] || make_dirty_repo "$repo" "$scenario_repo"
    fi
    if [[ -n "${GITUNIA_SCREENSHOT_CONFIG:-}" ]]; then
      # Optional config.json for the private config directory (options such as frameStyle).
      mkdir -p "$work/config/gitunia"
      cp "$GITUNIA_SCREENSHOT_CONFIG" "$work/config/gitunia/config.json"
    fi
    tmux_cmd kill-session -t shot 2>/dev/null || true
    tmux_cmd new-session -d -s shot -x "$columns" -y "$rows" "exec env COLORTERM=truecolor XDG_STATE_HOME=$(printf '%q' "$work/state") XDG_CONFIG_HOME=$(printf '%q' "$work/config") GIT_CONFIG_GLOBAL=/dev/null GIT_CONFIG_SYSTEM=/dev/null $(printf '%q %q' "$binary" "$scenario_repo")"
    wait_stable "$socket"
    if [ -n "$keys" ]; then
      # shellcheck disable=SC2086
      tmux_cmd send-keys -t shot $keys
      sleep 0.5
      wait_stable "$socket"
    fi
    if [[ "$scenario" == working-commit || "$scenario" == working-hook ]]; then
      type_message
      if [ "$scenario" = "working-hook" ]; then tmux_cmd send-keys -t shot M-Enter; fi
      sleep 1
      wait_stable "$socket"
    fi
    if [ -n "$keys_after" ]; then
      # The keys that need the loaded diff go after the first frame settled.
      # shellcheck disable=SC2086
      tmux_cmd send-keys -t shot $keys_after
      sleep 0.5
      wait_stable "$socket"
    fi
    if [ "$(tmux_cmd display-message -p -t shot '#{pane_dead}')" = "1" ]; then
      echo "the binary exited during scenario '$scenario'; no screenshot taken" >&2
      exit 1
    fi
    tmux_cmd capture-pane -t shot -p -e >"$work/$scenario.ansi"
    dotnet "$converter" --theme "$theme" --columns "$columns" --rows "$rows" <"$work/$scenario.ansi" >"$work/$scenario.svg"
    rsvg-convert "$work/$scenario.svg" -o "$out_dir/$scenario.png"
    echo "wrote $out_dir/$scenario.png" >&2
  done
}

wait_stable() { # wait_stable SOCKET: until three identical captures in a row (about 10 s at most)
  local socket="$1" previous="" current same=0 attempt
  for attempt in $(seq 100); do
    sleep 0.1
    current="$(tmux -L "$socket" capture-pane -t shot -p -e 2>/dev/null || true)"
    if [ -n "$current" ] && [ "$current" = "$previous" ]; then same=$((same + 1)); else same=0; fi
    previous="$current"
    [ "$same" -ge 3 ] && return 0
  done
  echo "frame did not settle within 10 s; capturing anyway" >&2
}

with_branch() { # with_branch DIR: clones the screenshots repository into DIR (a temporary clone, never touches this worktree)
  need gh "install the GitHub CLI"
  local dir="$1"
  gh repo clone "$(screenshots_slug)" "$dir" -- -q --branch "$branch_name" --single-branch --depth 1 \
    || { echo "cannot clone $(screenshots_slug); create it first (private, with a $branch_name branch)" >&2; exit 4; }
  git -C "$dir" config user.name "Claude (${GITUNIA_AGENT_ROLE:-implementer})"
  git -C "$dir" config user.email "$(git -C "$repo_root" config user.email)"
}

repository_slug() { # owner/name of origin, independent of the process working directory
  git -C "$repo_root" remote get-url origin | sed -E 's#^.*[:/]([^/:]+/[^/]+)$#\1#; s#\.git$##'
}

screenshots_slug() { # owner/name of the screenshots repository
  echo "${GITUNIA_SCREENSHOTS_REPO:-$(repository_slug)-screenshots}"
}

push_with_retry() { # push_with_retry DIR: plain push, rebased once on a concurrent publish or clean (never forced)
  git -C "$1" push -q origin "$branch_name" 2>/dev/null && return 0
  git -C "$1" pull -q --rebase origin "$branch_name"
  git -C "$1" push -q origin "$branch_name"
}

publish() {
  [ -n "$pull_request" ] || { echo "--pr <number> is required" >&2; exit 2; }
  ls "$out_dir"/*.png >/dev/null 2>&1 || { echo "no PNGs in $out_dir; run render first" >&2; exit 2; }
  local slug stamp target file pinned
  work="$(mktemp -d)"
  with_branch "$work/clone"
  slug="$(screenshots_slug)"
  stamp="$(git -C "$repo_root" rev-parse --short HEAD)"
  target="pr-$pull_request/$stamp"
  mkdir -p "$work/clone/$target"
  cp "$out_dir"/*.png "$work/clone/$target/"
  git -C "$work/clone" add -A
  if ! git -C "$work/clone" diff --cached --quiet; then
    git -C "$work/clone" commit -q -m "chore: screenshots for PR #$pull_request @ $stamp" -m "Agent: ${GITUNIA_AGENT_ROLE:-implementer}"
    push_with_retry "$work/clone"
  fi
  pinned="$(git -C "$work/clone" rev-parse HEAD)"
  echo "pushed to $slug ($target @ ${pinned:0:7})" >&2
  for file in "$out_dir"/*.png; do
    local name; name="$(basename "$file" .png)"
    echo "**$name**"
    echo
    echo "![$name](https://github.com/$slug/raw/$pinned/$target/$name.png)"
    echo
  done
}

clean() {
  local directory number state removed=0
  work="$(mktemp -d)"
  with_branch "$work/clone"
  for directory in "$work"/clone/pr-*; do
    [ -d "$directory" ] || continue
    number="${directory##*/pr-}"
    state="$(gh pr view "$number" -R "$(repository_slug)" --json state -q .state 2>/dev/null || echo UNKNOWN)"
    if [ "$state" = "MERGED" ] || [ "$state" = "CLOSED" ]; then
      git -C "$work/clone" rm -rq "pr-$number"
      removed=$((removed + 1))
      echo "removed pr-$number ($state)" >&2
    fi
  done
  if [ "$removed" -gt 0 ]; then
    git -C "$work/clone" commit -q -m "chore: remove screenshots of merged or closed PRs" -m "Agent: ${GITUNIA_AGENT_ROLE:-implementer}"
    push_with_retry "$work/clone"
  else
    echo "nothing to clean" >&2
  fi
}

case "$command_name" in
  render) render ;;
  publish) publish ;;
  clean) clean ;;
  *) usage ;;
esac
