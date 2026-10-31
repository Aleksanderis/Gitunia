# README demo tape (VHS). Run it through scripts/demo.sh, which sets DEMO_BIN and DEMO_REPO.
Output demo.gif
Output demo.webm
Set Shell "bash"
Set FontSize 16
Set Width 1400
Set Height 760
Set Padding 8
Set Framerate 20
Set TypingSpeed 70ms
Set Theme "Catppuccin Mocha"

Hide
Type 'clear; exec "$DEMO_BIN" "$DEMO_REPO"'
Enter
Sleep 2s
Show

# The graph: walk down and back up.
Sleep 1s
Type "jjjj"
Sleep 700ms
Type "kk"
Sleep 700ms

# Branch switch: the checkout picker.
Type " bs"
Sleep 700ms
Type "fix"
Sleep 900ms
Enter
Sleep 1500ms

# Worktree: add one for another branch (the sidebar owns the worktree keys).
Ctrl+w
Type "h"
Sleep 500ms
Type " wa"
Sleep 800ms
Type "feature/multiply"
Sleep 600ms
Enter
Sleep 800ms
Enter
Sleep 1500ms

# Working tree: stage a few lines, then commit.
Type " s"
Sleep 1200ms
Enter
Sleep 800ms
Type "jjjjj"
Type "v"
Type "j"
Sleep 600ms
Type " a"
Sleep 1200ms
Type " c"
Sleep 700ms
Type "Document division and modulo"
Sleep 500ms
Ctrl+g
Sleep 1500ms
