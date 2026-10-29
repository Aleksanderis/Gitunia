set -e
export GIT_CONFIG_GLOBAL=/dev/null GIT_CONFIG_SYSTEM=/dev/null GIT_AUTHOR_NAME=A GIT_AUTHOR_EMAIL=a@e.c GIT_COMMITTER_NAME=A GIT_COMMITTER_EMAIL=a@e.c
rm -rf "$1"; mkdir -p "$1"; cd "$1"
git init -q -b main .
printf '{\n  "a": 1,\n  "b": 2\n}\n' > data.json; echo x>other; git add -A; git commit -qm base
git checkout -qb fix/new-bug-of-smth; sed -i 's/"a": 1/"a": 10/' data.json; git commit -qam feat
git checkout -q main; sed -i 's/"a": 1/"a": 11/' data.json; git commit -qam main2
git checkout -q fix/new-bug-of-smth; git rebase main >/dev/null 2>&1 || true
git status --short
