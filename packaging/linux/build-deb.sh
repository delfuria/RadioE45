#!/usr/bin/env bash
# Builds the Linux .deb of the Uno desktop app (RadioE45.Uno, net10.0-desktop).
#
#   packaging/linux/build-deb.sh <linux-x64|linux-arm64> <output-dir>
#
# Runs on macOS (brew install dpkg) and on Linux (dpkg-deb is part of dpkg).
# Output: <output-dir>/radioe45_<version>_<amd64|arm64>.deb, version from version.json (NBGV).
# The package installs the self-contained app in /opt/radioe45 and links /usr/bin/radioe45;
# libVLC comes from the system (Depends in control.in). See docs/LINUX-UNO-ROADMAP.md, S10.0.
set -euo pipefail

RID="${1:-}"
OUT_DIR="${2:-}"
case "$RID" in
    linux-x64)   ARCH=amd64 ;;
    linux-arm64) ARCH=arm64 ;;
    *) echo "usage: $0 <linux-x64|linux-arm64> <output-dir>" >&2; exit 2 ;;
esac
[ -n "$OUT_DIR" ] || { echo "usage: $0 <linux-x64|linux-arm64> <output-dir>" >&2; exit 2; }

command -v dpkg-deb >/dev/null || { echo "dpkg-deb not found (macOS: brew install dpkg)" >&2; exit 1; }

PACKAGING_DIR="$(cd "$(dirname "$0")" && pwd)"
REPO_ROOT="$(cd "$PACKAGING_DIR/../.." && pwd)"
PROJECT="$REPO_ROOT/RadioE45.Uno/RadioE45.Uno.csproj"
TFM=net10.0-desktop

# NBGV needs the full git history (CI: fetch-depth 0).
VERSION="$(dotnet msbuild "$PROJECT" -nologo -target:GetBuildVersion \
    -getProperty:BuildVersionSimple -p:TargetFramework=$TFM | tr -d '[:space:]')"
[[ "$VERSION" =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]] || { echo "unexpected version '$VERSION'" >&2; exit 1; }

WORK_DIR="$(mktemp -d "${TMPDIR:-/tmp}/radioe45-deb.XXXXXX")"
trap 'rm -r "$WORK_DIR"' EXIT
PUBLISH_DIR="$WORK_DIR/publish"
ROOT="$WORK_DIR/radioe45_${VERSION}_${ARCH}"
DEB="radioe45_${VERSION}_${ARCH}.deb"

echo "==> Radio E45 $VERSION, $RID ($ARCH)"

echo "==> dotnet publish"
dotnet publish "$PROJECT" -f $TFM -c Release -r "$RID" --self-contained \
    -p:DebugType=None -p:DebugSymbols=false -o "$PUBLISH_DIR" -nologo -v quiet

echo "==> package tree"
mkdir -p "$ROOT/DEBIAN" "$ROOT/opt" "$ROOT/usr/bin" \
    "$ROOT/usr/share/applications" "$ROOT/usr/share/metainfo"
cp -R "$PUBLISH_DIR" "$ROOT/opt/radioe45"
# Static SQLite library, only used to link native AOT builds.
find "$ROOT/opt/radioe45" -name '*.a' -delete
ln -s /opt/radioe45/RadioE45.Uno "$ROOT/usr/bin/radioe45"

cp "$PACKAGING_DIR/com.radioe45.app.desktop" "$ROOT/usr/share/applications/"
cp "$PACKAGING_DIR/com.radioe45.app.metainfo.xml" "$ROOT/usr/share/metainfo/"
for size in 48 64 128 256 512; do
    mkdir -p "$ROOT/usr/share/icons/hicolor/${size}x${size}/apps"
    cp "$PACKAGING_DIR/icons/radioe45-$size.png" \
        "$ROOT/usr/share/icons/hicolor/${size}x${size}/apps/radioe45.png"
done

# Normalized permissions: the files come from a macOS or CI checkout with any umask.
find "$ROOT" -type d -exec chmod 755 {} +
find "$ROOT" -type f -exec chmod 644 {} +
chmod 755 "$ROOT/opt/radioe45/RadioE45.Uno"

INSTALLED_SIZE="$(du -sk "$ROOT" | cut -f1)"
sed -e "s/@VERSION@/$VERSION/" -e "s/@ARCH@/$ARCH/" -e "s/@INSTALLED_SIZE@/$INSTALLED_SIZE/" \
    "$PACKAGING_DIR/control.in" > "$ROOT/DEBIAN/control"

echo "==> dpkg-deb"
mkdir -p "$OUT_DIR"
dpkg-deb --build --root-owner-group -Zxz "$ROOT" "$OUT_DIR/$DEB"
dpkg-deb --info "$OUT_DIR/$DEB" | sed -n '/Package:/,/Description:/p'
ls -lh "$OUT_DIR/$DEB"
