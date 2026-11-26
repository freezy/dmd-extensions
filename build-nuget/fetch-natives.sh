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

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
STAGE="$ROOT/native/nuget/runtimes"
TMP="$(mktemp -d)"
mkdir -p "$STAGE"
NEW="$(mktemp -d "$STAGE/.new.XXXXXX")"
trap 'rm -rf "$TMP" "$NEW"' EXIT

. "$SCRIPT_DIR/natives-sources.sh"

DIGESTS="$SCRIPT_DIR/natives.sha256"

requested=""
for rid in $RIDS; do
  case " $ALL_RIDS " in
    *" $rid "*) ;;
    *) echo "unknown rid: $rid (known: $ALL_RIDS)" >&2; exit 1 ;;
  esac
  case " $requested " in
    *" $rid "*) ;;
    *) requested="$requested $rid" ;;
  esac
done
RIDS="${requested# }"

verify_archive() { # archive name
  local expected actual
  expected="$(awk -v name="$2" '$2 == name { print $1 }' "$DIGESTS")"
  if [ -z "$expected" ]; then
    echo "$2 is missing from build-nuget/natives.sha256." >&2
    echo "After changing a version in natives-sources.sh, run update-digests.sh and commit the result." >&2
    exit 1
  fi
  actual="$(sha256_of "$1")"
  if [ "$actual" != "$expected" ]; then
    echo "$2 does not match build-nuget/natives.sha256." >&2
    echo "  recorded $expected" >&2
    echo "  computed $actual" >&2
    echo "The archive behind that URL changed. Establish why before updating the record." >&2
    exit 1
  fi
}

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
  dest="$NEW/$rid/native"
  sdir="$TMP/serum-$rid"; zdir="$TMP/zedmd-$rid"

  sname="$(serum_archive "$rid")"
  zname="$(zedmd_archive "$rid")"
  surl="$(serum_url "$rid")"
  zurl="$(zedmd_url "$rid")"
  # keep the real extension (.zip / .tar.gz) so extract() can detect the archive type
  sarch="$TMP/serum-${rid}-${sname}"
  zarch="$TMP/zedmd-${rid}-${zname}"

  echo "[$rid] downloading libserum  $surl"
  curl -fsSL "$surl" -o "$sarch"
  verify_archive "$sarch" "$sname"
  echo "[$rid] downloading libzedmd  $zurl"
  curl -fsSL "$zurl" -o "$zarch"
  verify_archive "$zarch" "$zname"

  mkdir -p "$dest"
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
      # libzedmd needs libserialport and libsockpp by their sonames (libserialport.so.0,
      # libsockpp.so.1). The tarball has those as symlinks, which Git Bash can't extract.
      for lib in libserialport libsockpp; do
        versioned="$(ls "$dest/$lib".so.*.*.* 2>/dev/null | head -1)"
        if [ -n "$versioned" ]; then
          soname="${versioned%.*.*}"
          [ -f "$soname" ] || cp -fL "$versioned" "$soname"
        fi
      done
      # The serial port shim P/Invokes "libserialport".
      [ -f "$dest/libserialport.so" ] || cp -fL "$dest/libserialport.so.0" "$dest/libserialport.so"
      ;;
  esac

  # Vendored extra natives: libusb-1.0 (PIN2DMD / PinDMD2) and libftd2xx (PinDMD1) don't have a
  # single canonical upstream release like serum/zedmd, so they're sourced from a vendored drop.
  # Place the per-OS binaries under native/vendor/<rid>/ using the resolver's canonical names
  # (libusb-1.0.dll / libusb-1.0.0.dylib / libusb-1.0.so.0; ftd2xx.dll / libftd2xx.dylib /
  # libftd2xx.so) and they get packaged here. See native/vendor/README.md.
  vendor="$ROOT/native/vendor/$rid"
  if [ -d "$vendor" ]; then
    echo "[$rid] staging vendored natives from native/vendor/$rid"
    copy_libs "$vendor" "$dest"
  fi

  echo "[$rid] staged: $(cd "$dest" && ls | tr '\n' ' ')"

  # Sanity: libusb-1.0 is required by PIN2DMD / PinDMD2 and is vendored (not downloaded), so warn
  # loudly if it didn't get staged — otherwise the package ships fine but fails at the consumer's
  # first P/Invoke with DllNotFoundException. Non-fatal: serum/zedmd-only packages are still valid.
  ls "$dest"/libusb-1.0.* >/dev/null 2>&1 \
    || echo "[$rid] WARNING: libusb-1.0 not staged — PIN2DMD/PinDMD2 will fail at runtime. Drop it under native/vendor/$rid/ (see native/vendor/README.md)."
done

mkdir -p "$STAGE"
for rid in $RIDS; do
  rm -rf "$STAGE/$rid"
  mv "$NEW/$rid" "$STAGE/$rid"
done

echo "Done. Native binaries staged under native/nuget/runtimes/ (libserum $SERUM_VERSION, libzedmd $ZEDMD_VERSION; plus any native/vendor/ drops)."
