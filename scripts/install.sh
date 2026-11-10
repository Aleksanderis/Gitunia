#!/bin/sh
# Installs or updates Gitunia from the latest GitHub release (#1248).
#
#   curl -fsSL https://raw.githubusercontent.com/Aleksanderis/Gitunia/main/scripts/install.sh | sh
#
# Linux (x64, arm64): uses the native package when the system has pacman, apt or dnf and root (or sudo), else puts the
# release archive's binary into ~/.local/bin (no root). Every download is checked against its .sha256 file. Running it
# again updates; it does nothing when the installed version is already the latest.
# macOS (arm64): archive only, when the release has one.
#
# Environment (all optional):
#   GITUNIA_METHOD       package | archive    (default: package when possible, else archive)
#   GITUNIA_INSTALL_DIR  where the archive's binary goes (default: ~/.local/bin)
#   GITUNIA_FORCE        1 = install even when the latest version is already installed
#   GITUNIA_RELEASE_URL  release JSON to read (default: the GitHub API's latest release; a file:// URL works, for tests)
#   GITUNIA_RETRY_DELAY  seconds before the first download retry, grows per retry (default 2; 0 in tests)
#   GITUNIA_OS, GITUNIA_ARCH  override uname -s and uname -m (for tests)
#
# POSIX sh, no jq. The whole script is one function called on the last line, so a cut-off download runs nothing.
set -eu

REPOSITORY="Aleksanderis/Gitunia"
DEFAULT_RELEASE_URL="https://api.github.com/repos/$REPOSITORY/releases/latest"

say() { printf '%s\n' "$*"; }
fail() { printf 'gitunia install: %s\n' "$*" >&2; exit 1; }
have() { command -v "$1" >/dev/null 2>&1; }

# Downloads $1 to the file $2 (curl or wget; file:// URLs work with curl). $3 is an optional hint added to the failure.
# Tries up to 4 times with a growing pause (GitHub answers 502/504 now and then); GITUNIA_RETRY_DELAY is the first pause in seconds.
download() {
  attempt=1
  while :; do
    if have curl; then curl -fsSL -o "$2" "$1" && return 0
    elif have wget; then wget -q -O "$2" "$1" && return 0
    else fail "need curl or wget"; fi
    [ "$attempt" -lt 4 ] || fail "download failed after $attempt attempts: $1 ${3:-}(releases: https://github.com/$REPOSITORY/releases)"
    pause=$((attempt * ${GITUNIA_RETRY_DELAY:-2}))
    printf 'gitunia install: download failed, retrying in %ss: %s\n' "$pause" "$1" >&2
    sleep "$pause"
    attempt=$((attempt + 1))
  done
}

sha256_of() {
  if have sha256sum; then sha256sum "$1" | cut -d ' ' -f 1
  elif have shasum; then shasum -a 256 "$1" | cut -d ' ' -f 1
  else fail "need sha256sum or shasum to verify the download"; fi
}

# Prints the download URL of the release asset whose name ends with $2 (the release JSON is in the file $1).
asset_url() {
  grep -o '"browser_download_url": *"[^"]*"' "$1" | cut -d '"' -f 4 |
    awk -v suffix="$2" 'length($0) >= length(suffix) && substr($0, length($0) - length(suffix) + 1) == suffix { print; exit }'

}

# Downloads asset URL $1 into directory $2 and checks it against its .sha256 asset; prints the local path.
fetch_verified() {
  name="${1##*/}"
  download "$1" "$2/$name"
  download "$1.sha256" "$2/$name.sha256"
  expected="$(cut -d ' ' -f 1 "$2/$name.sha256" | head -n 1)"
  actual="$(sha256_of "$2/$name")"
  [ -n "$expected" ] && [ -n "$actual" ] || fail "no checksum for $name"
  [ "$expected" = "$actual" ] || fail "checksum mismatch for $name (expected $expected, got $actual)"
  printf '%s' "$2/$name"
}

# Runs a command as root: directly when we are root, else through sudo; fails when neither is possible.
as_root() {
  if [ "$(id -u)" = "0" ]; then "$@"
  elif have sudo; then sudo "$@"
  else return 1; fi
}

can_be_root() { [ "$(id -u)" = "0" ] || have sudo; }

install_package() {
  # $1 = release JSON, $2 = work dir, $3 = x64 | arm64. Returns 1 when no native package fits this system.
  json="$1"; work="$2"; arch="$3"
  if [ "$arch" = "x64" ]; then pacman_suffix="-x86_64.pkg.tar.zst"; deb_suffix="_amd64.deb"; rpm_suffix=".x86_64.rpm"
  else pacman_suffix="-aarch64.pkg.tar.zst"; deb_suffix="_arm64.deb"; rpm_suffix=".aarch64.rpm"; fi

  can_be_root || return 1
  if have pacman; then suffix="$pacman_suffix"; elif have apt-get; then suffix="$deb_suffix"
  elif have dnf; then suffix="$rpm_suffix"; else return 1; fi
  url="$(asset_url "$json" "$suffix")"
  [ -n "$url" ] || return 1

  # Not "set -e" here: this runs in an if condition, so a failed verification must stop the script explicitly.
  file="$(fetch_verified "$url" "$work")" || exit 1
  [ -n "$file" ] || exit 1
  say "Installing $(basename "$file") (asks for your password if needed)"
  # A fitting package that fails to install is an error, not a reason to fall back silently.
  hint="(no root or sudo rights? run again with GITUNIA_METHOD=archive)"
  if have pacman; then as_root pacman -U --noconfirm "$file" || fail "pacman could not install $file $hint"
  elif have apt-get; then as_root apt-get install -y "$file" || fail "apt-get could not install $file $hint"
  else as_root dnf install -y "$file" || fail "dnf could not install $file $hint"; fi
}

install_archive() {
  # $1 = release JSON, $2 = work dir, $3 = runtime id suffix (linux-x64, linux-arm64, osx-arm64).
  json="$1"; work="$2"; rid="$3"
  url="$(asset_url "$json" "-$rid.tar.gz")"
  [ -n "$url" ] || fail "this release has no $rid archive yet; see https://github.com/$REPOSITORY/releases"
  have tar || fail "need tar"

  file="$(fetch_verified "$url" "$work")" || exit 1
  mkdir -p "$work/unpacked"
  tar -xzf "$file" -C "$work/unpacked"
  binary="$(find "$work/unpacked" -type f -name gitunia | head -n 1)"
  [ -n "$binary" ] || fail "the archive holds no gitunia binary"

  target="${GITUNIA_INSTALL_DIR:-$HOME/.local/bin}"
  mkdir -p "$target"
  # A temporary name and a rename replace a running binary safely.
  cp "$binary" "$target/gitunia.new"
  chmod 755 "$target/gitunia.new"
  mv -f "$target/gitunia.new" "$target/gitunia"
  # The install-source marker (#1291): tells `gitunia update` that this binary may replace itself.
  printf 'script\n' > "$target/gitunia.install-source"
  say "Installed $target/gitunia"
  case ":$PATH:" in
    *":$target:"*) ;;
    *) say "Note: $target is not on your PATH; add it to your shell profile." ;;
  esac
}

main() {
  os="${GITUNIA_OS:-$(uname -s)}"
  machine="${GITUNIA_ARCH:-$(uname -m)}"
  case "$machine" in
    x86_64|amd64) arch="x64" ;;
    aarch64|arm64) arch="arm64" ;;
    *) fail "unsupported architecture: $machine (releases exist for x64 and arm64)" ;;
  esac
  case "$os" in
    Linux)
      if have ldd && ldd --version 2>&1 | grep -qi musl; then
        fail "the Linux builds need glibc; this system uses musl (Alpine)"
      fi
      rid="linux-$arch" ;;
    Darwin)
      [ "$arch" = "arm64" ] || fail "macOS builds exist for Apple silicon only"
      rid="osx-arm64" ;;
    *) fail "unsupported system: $os (Windows: use install.ps1 or winget)" ;;
  esac

  have git || say "Note: Gitunia needs git 2.43 or newer on your PATH; git was not found."

  work="$(mktemp -d)"
  trap 'rm -rf "$work"' EXIT INT TERM HUP
  json="$work/release.json"
  download "${GITUNIA_RELEASE_URL:-$DEFAULT_RELEASE_URL}" "$json" "(GitHub rate limit? try again later) "
  tag="$(grep -o '"tag_name": *"[^"]*"' "$json" | head -n 1 | cut -d '"' -f 4)"
  [ -n "$tag" ] || fail "could not read the latest release from $DEFAULT_RELEASE_URL"
  latest="${tag#v}"

  # The copy this run would replace: the archive folder for an archive install, else the one on the PATH.
  if [ "${GITUNIA_METHOD:-package}" = "archive" ]; then current="${GITUNIA_INSTALL_DIR:-$HOME/.local/bin}/gitunia"
  else current="$(command -v gitunia || true)"; fi
  if [ -n "$current" ] && [ -x "$current" ] && [ "${GITUNIA_FORCE:-0}" != "1" ]; then
    installed="$("$current" --version 2>/dev/null | awk '{print $2; exit}' || true)"
    if [ "$installed" = "$latest" ]; then
      say "Gitunia $latest is already installed."
      return 0
    fi
    [ -z "$installed" ] || say "Updating Gitunia $installed -> $latest"
  fi

  method="${GITUNIA_METHOD:-package}"
  case "$method" in
    package)
      if [ "$os" = "Linux" ] && install_package "$json" "$work" "$arch"; then :
      else install_archive "$json" "$work" "$rid"; fi ;;
    archive) install_archive "$json" "$work" "$rid" ;;
    *) fail "GITUNIA_METHOD must be package or archive" ;;
  esac
  say "Gitunia $latest installed. Run: gitunia"
}

main "$@"
