#!/usr/bin/env bash
#
# Rewrites natives.sha256 for the libserum and libzedmd versions pinned in natives-sources.sh.
# Run it after a version bump, then review the diff before committing.
#
# Requires: bash 4+, curl, and sha256sum or shasum.
#
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
. "$SCRIPT_DIR/natives-sources.sh"

OUT="$SCRIPT_DIR/natives.sha256"
TMP="$(mktemp -d)"
CANDIDATE="$(mktemp "$OUT.XXXXXX")"
chmod 644 "$CANDIDATE"
trap 'rm -rf "$TMP" "$CANDIDATE"' EXIT

record() { # name url
  local digest
  echo "downloading $1" >&2
  curl -fsSL "$2" -o "$TMP/$1"
  digest="$(sha256_of "$TMP/$1")"
  printf '%s  %s\n' "$digest" "$1" >> "$TMP/digests"
}

: > "$TMP/digests"
for rid in $ALL_RIDS; do
  record "$(serum_archive "$rid")" "$(serum_url "$rid")"
  record "$(zedmd_archive "$rid")" "$(zedmd_url "$rid")"
done

sort -u -k2 "$TMP/digests" > "$CANDIDATE"
mv "$CANDIDATE" "$OUT"
echo "wrote natives.sha256 for libserum $SERUM_VERSION and libzedmd $ZEDMD_VERSION" >&2
cat "$OUT"
