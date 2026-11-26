# Where the native archives come from. Sourced by fetch-natives.sh and update-digests.sh.
#
# Env overrides: SERUM_VERSION, ZEDMD_VERSION, RIDS

SERUM_VERSION="${SERUM_VERSION:-2.8.2}"
ZEDMD_VERSION="${ZEDMD_VERSION:-0.11.0}"

ALL_RIDS="win-x64 osx-x64 osx-arm64 linux-x64"
RIDS="${RIDS:-$ALL_RIDS}"

# rid -> upstream archive suffix
serum_asset() { case "$1" in
  win-x64)   echo "win-x64.zip" ;;
  osx-x64)   echo "macos-x64.tar.gz" ;;
  osx-arm64) echo "macos-arm64.tar.gz" ;;
  linux-x64) echo "linux-x64.tar.gz" ;;
  *) echo "" ;; esac; }
zedmd_asset() { case "$1" in
  win-x64)   echo "win-x64-Release.zip" ;;
  osx-x64)   echo "macos-x64.tar.gz" ;;
  osx-arm64) echo "macos-arm64.tar.gz" ;;
  linux-x64) echo "linux-x64.tar.gz" ;;
  *) echo "" ;; esac; }

serum_archive() { echo "libserum-${SERUM_VERSION}-$(serum_asset "$1")"; }
zedmd_archive() { echo "libzedmd-${ZEDMD_VERSION}-$(zedmd_asset "$1")"; }
serum_url() { echo "https://github.com/PPUC/libserum/releases/download/v${SERUM_VERSION}/$(serum_archive "$1")"; }
zedmd_url() { echo "https://github.com/PPUC/libzedmd/releases/download/v${ZEDMD_VERSION}/$(zedmd_archive "$1")"; }

sha256_of() { # file
  if command -v sha256sum >/dev/null 2>&1; then
    sha256sum "$1" | cut -d' ' -f1
  elif command -v shasum >/dev/null 2>&1; then
    shasum -a 256 "$1" | cut -d' ' -f1
  else
    echo "Needs sha256sum or shasum. Install coreutils (Debian: apt install coreutils, macOS: brew install coreutils)." >&2
    exit 1
  fi
}
