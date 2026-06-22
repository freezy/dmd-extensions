#!/usr/bin/env bash
#
# Downloads the per-platform native binaries (libserum, libzedmd) from their GitHub releases
# and stages them under native/nuget/runtimes/<rid>/native/ so the LibDmd.Core.Native.<rid>
# packages can pick them up.
#
# Env overrides: SERUM_VERSION, ZEDMD_VERSION, RIDS
#
# Requires: bash 4+, curl, unzip, tar. Runs on Linux/macOS and on Windows via Git Bash.
#
set -euo pipefail

SERUM_VERSION="${SERUM_VERSION:-2.5.2}"
ZEDMD_VERSION="${ZEDMD_VERSION:-0.11.0}"
RIDS="${RIDS:-win-x64 osx-x64 osx-arm64 linux-x64}"

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
STAGE="$ROOT/native/nuget/runtimes"
TMP="$(mktemp -d)"
trap 'rm -rf "$TMP"' EXIT

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

extract() { # archive destdir
  mkdir -p "$2"
  case "$1" in
    *.zip)    unzip -o -q "$1" -d "$2" ;;
    # '|| true': Git Bash on Windows can't create the tarball's symlinks; the real (versioned)
    # files still extract, which is all copy_libs needs. On Linux/macOS CI this is lossless.
    *.tar.gz) tar -xzf "$1" -C "$2" 2>/dev/null || true ;;
    *) echo "unknown archive: $1" >&2; exit 1 ;;
  esac
}

# copy shared libraries (deref symlinks to real files), skipping static libs / exes / test data
copy_libs() { # srcdir destdir
  find "$1" -type f \( -name '*.dll' -o -name '*.so' -o -name '*.so.*' -o -name '*.dylib' \) \
    ! -path '*/test/*' -print0 | while IFS= read -r -d '' f; do
    cp -fL "$f" "$2/"
  done
}

for rid in $RIDS; do
  dest="$STAGE/$rid/native"
  rm -rf "$dest"; mkdir -p "$dest"
  sdir="$TMP/serum-$rid"; zdir="$TMP/zedmd-$rid"

  sasset="$(serum_asset "$rid")"
  zasset="$(zedmd_asset "$rid")"
  surl="https://github.com/PPUC/libserum/releases/download/v${SERUM_VERSION}/libserum-${SERUM_VERSION}-${sasset}"
  zurl="https://github.com/PPUC/libzedmd/releases/download/v${ZEDMD_VERSION}/libzedmd-${ZEDMD_VERSION}-${zasset}"
  # keep the real extension (.zip / .tar.gz) so extract() can detect the archive type
  sarch="$TMP/serum-${rid}-${sasset}"
  zarch="$TMP/zedmd-${rid}-${zasset}"

  echo "[$rid] downloading libserum  $surl"
  curl -fsSL "$surl" -o "$sarch"
  echo "[$rid] downloading libzedmd  $zurl"
  curl -fsSL "$zurl" -o "$zarch"

  extract "$sarch" "$sdir"
  extract "$zarch" "$zdir"
  copy_libs "$sdir" "$dest"
  copy_libs "$zdir" "$dest"

  # Ensure the primary libs carry the canonical name the NativeLibraryLoader resolver expects
  # (serum64.dll/zedmd64.dll on Windows; libserum/libzedmd.{dylib,so} elsewhere).
  case "$rid" in
    osx-*)
      [ -f "$dest/libserum.dylib" ] || cp -fL "$(ls "$dest"/libserum*.dylib | head -1)" "$dest/libserum.dylib"
      [ -f "$dest/libzedmd.dylib" ] || cp -fL "$(ls "$dest"/libzedmd*.dylib | head -1)" "$dest/libzedmd.dylib"
      ;;
    linux-*)
      [ -f "$dest/libserum.so" ] || cp -fL "$(ls "$dest"/libserum.so* | head -1)" "$dest/libserum.so"
      [ -f "$dest/libzedmd.so" ] || cp -fL "$(ls "$dest"/libzedmd.so* | head -1)" "$dest/libzedmd.so"
      ;;
  esac

  echo "[$rid] staged: $(cd "$dest" && ls | tr '\n' ' ')"
done

echo "Done. Native binaries staged under native/nuget/runtimes/ (libserum $SERUM_VERSION, libzedmd $ZEDMD_VERSION)."
