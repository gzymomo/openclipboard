#!/usr/bin/env bash
# Build a distributable OpenClipboard.dmg from a macOS publish directory.
#
# Usage:
#   installer/macos/package.sh <publish-dir> <version> <output-dmg>
#
# The publish must be self-contained and produced with
# -p:AssemblyName=OpenClipboard (see .github/workflows/macos.yml).
set -euo pipefail

PUBLISH_DIR="${1:?usage: package.sh <publish-dir> <version> <output-dmg>}"
VERSION="${2:?usage: package.sh <publish-dir> <version> <output-dmg>}"
OUTPUT_DMG="${3:?usage: package.sh <publish-dir> <version> <output-dmg>}"

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
APP_NAME="OpenClipboard"
ICON_SOURCE="$ROOT/src/OpenClipboard.App/Assets/logo.png"

if [[ ! -d "$PUBLISH_DIR" ]]; then
  echo "Publish directory not found: $PUBLISH_DIR" >&2
  exit 1
fi

if [[ ! -f "$PUBLISH_DIR/OpenClipboard.App" && ! -f "$PUBLISH_DIR/$APP_NAME" ]]; then
  echo "Expected executable 'OpenClipboard.App' or '$APP_NAME' in $PUBLISH_DIR." >&2
  exit 1
fi

WORK_DIR="$(mktemp -d)"
trap 'rm -rf "$WORK_DIR"' EXIT

APP="$WORK_DIR/$APP_NAME.app"
mkdir -p "$APP/Contents/MacOS" "$APP/Contents/Resources"

cp -R "$PUBLISH_DIR"/. "$APP/Contents/MacOS/"

# The published apphost is OpenClipboard.App. Renaming it inside the bundle is
# safe (the host keeps the embedded OpenClipboard.App.dll name) and avoids a
# fatal Gatekeeper issue: macOS treats any executable ending in .App as an
# application bundle and SIGKILLs it when no bundle structure/notarization exists.
if [[ -f "$APP/Contents/MacOS/OpenClipboard.App" ]]; then
  mv "$APP/Contents/MacOS/OpenClipboard.App" "$APP/Contents/MacOS/$APP_NAME"
fi

sed "s/__APP_VERSION__/$VERSION/g" "$ROOT/installer/macos/Info.plist" > "$APP/Contents/Info.plist"
plutil -lint "$APP/Contents/Info.plist" >/dev/null

if [[ -f "$ICON_SOURCE" ]]; then
  ICONSET="$WORK_DIR/$APP_NAME.iconset"
  mkdir -p "$ICONSET"
  for size in 16 32 128 256 512; do
    sips -z "$size" "$size" "$ICON_SOURCE" --out "$ICONSET/icon_${size}x${size}.png" >/dev/null
    sips -z "$((size * 2))" "$((size * 2))" "$ICON_SOURCE" --out "$ICONSET/icon_${size}x${size}@2x.png" >/dev/null
  done
  iconutil -c icns "$ICONSET" -o "$APP/Contents/Resources/$APP_NAME.icns"
else
  echo "Icon source not found, shipping without an icon: $ICON_SOURCE" >&2
fi

# The main binary name must not end in .app: macOS would assess it as a bundle
# and Gatekeeper would SIGKILL it before the .NET host ever starts.
test -x "$APP/Contents/MacOS/$APP_NAME"

xattr -cr "$APP" 2>/dev/null || true

codesign --force --deep --sign - "$APP"
codesign --verify --deep --strict --verbose=2 "$APP"

# Stage the signed bundle with an Applications shortcut for drag-to-install.
STAGE="$WORK_DIR/dmg-root"
mkdir -p "$STAGE"
# ditto preserves extended attributes and the codesign signature resources.
ditto "$APP" "$STAGE/$APP_NAME.app"
ln -s /Applications "$STAGE/Applications"

mkdir -p "$(dirname "$OUTPUT_DMG")"
rm -f "$OUTPUT_DMG"

# hdiutil ships with macOS; UDZO is a compressed, read-only disk image.
hdiutil create -volname "$APP_NAME" -srcfolder "$STAGE" -ov -format UDZO "$OUTPUT_DMG"

echo "Created $OUTPUT_DMG"
