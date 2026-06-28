#!/usr/bin/env bash
#
# Publishes the LibDmd.Core harness as a self-contained macOS app so someone can test the
# SDL / GL-ES (ANGLE) virtual-DMD window on a Mac WITHOUT installing the .NET SDK or Unity.
#
# Usage:   ./publish-macos.sh [osx-arm64|osx-x64]     (default: osx-arm64)
# Output:  artifacts/macos-harness/<rid>/             (+ a .tar.gz next to it)
#
# SDL2 + ANGLE (libEGL/libGLESv2) are NOT redistributed in this repo. Drop the three dylibs into
# native/vendor/<rid>/ and they get bundled automatically; otherwise the script warns and the
# tester must place them next to the executable. See README-macos-test.md.
#
# Works from Windows (Git Bash) or macOS — `dotnet publish -r osx-...` cross-publishes fine.
set -euo pipefail

RID="${1:-osx-arm64}"
case "$RID" in osx-arm64|osx-x64) ;; *) echo "Unsupported RID '$RID' (use osx-arm64 or osx-x64)" >&2; exit 1 ;; esac

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
OUT="$ROOT/artifacts/macos-harness/$RID"
VENDOR="$ROOT/native/vendor/$RID"

echo "==> Publishing LibDmd.Core.Harness ($RID, self-contained)"
rm -rf "$OUT"
dotnet publish "$SCRIPT_DIR/LibDmd.Core.Harness.csproj" -c Release -r "$RID" --self-contained -o "$OUT" -p:RuntimeIdentifier="$RID"

# Bundle the windowing natives the harness needs on macOS (not shipped in-repo).
echo "==> Bundling SDL2 + ANGLE from native/vendor/$RID (if present)"
missing=()
for f in libSDL2.dylib libEGL.dylib libGLESv2.dylib; do
	if [ -f "$VENDOR/$f" ]; then
		cp -f "$VENDOR/$f" "$OUT/"; echo "    bundled $f"
	else
		missing+=("$f")
	fi
done
if [ ${#missing[@]} -gt 0 ]; then
	echo "    WARNING: not bundled (drop into native/vendor/$RID/, or place next to the exe on the Mac):"
	echo "             ${missing[*]}"
fi

echo "==> Packaging"
( cd "$ROOT/artifacts/macos-harness" && tar -czf "libdmd-macos-harness-$RID.tar.gz" "$RID" )
echo "==> Done: $ROOT/artifacts/macos-harness/libdmd-macos-harness-$RID.tar.gz"
echo "    Tester runs (after unpacking):  xattr -dr com.apple.quarantine .  &&  ./LibDmd.Core.Harness --native-window"
