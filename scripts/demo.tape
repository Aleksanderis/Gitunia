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

# The graph: scroll down a long history (half pages, then single rows), jump to the bottom and back, then to HEAD.
Sleep 1s
Type "jjjjjjjjjjjj"
Sleep 600ms
Ctrl+d
Sleep 500ms
Ctrl+d
Sleep 500ms
Ctrl+d
Sleep 500ms
Type "jjjjjjjj"
Sleep 600ms
Type "G"
Sleep 1200ms
Type "gg"
Sleep 800ms
Type "jjjjkk"
Sleep 700ms

# Branch switch: the checkout picker.
Type " bs"
Sleep 700ms
Type "fix/over"
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
