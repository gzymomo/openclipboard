#!/usr/bin/env bash
# Build a .deb package from a Linux self-contained publish directory.
#
# Usage:
#   installer/linux/package-deb.sh <publish-dir> <version> <arch> <output-deb>
#
# arch: amd64 | arm64
set -euo pipefail

PUBLISH_DIR="${1:?usage: package-deb.sh <publish-dir> <version> <arch> <output-deb>}"
VERSION="${2:?usage: package-deb.sh <publish-dir> <version> <arch> <output-deb>}"
ARCH="${3:?usage: package-deb.sh <publish-dir> <version> <arch> <output-deb>}"
OUTPUT_DEB="${4:?usage: package-deb.sh <publish-dir> <version> <arch> <output-deb>}"

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
APP_ID="openclipboard"
APP_NAME="OpenClipboard"
INSTALL_DIR="/opt/openclipboard"
ICON_SOURCE="$ROOT/src/Clipboard.Desktop/resources/images/logo.png"

case "$ARCH" in
  amd64|arm64) ;;
  *)
    echo "Unsupported arch: $ARCH (expected amd64 or arm64)" >&2
    exit 1
    ;;
esac

if [[ ! -d "$PUBLISH_DIR" ]]; then
  echo "Publish directory not found: $PUBLISH_DIR" >&2
  exit 1
fi

if [[ ! -f "$PUBLISH_DIR/OpenClipboard.App" ]]; then
  echo "Expected executable 'OpenClipboard.App' in $PUBLISH_DIR" >&2
  exit 1
fi

WORK_DIR="$(mktemp -d)"
trap 'rm -rf "$WORK_DIR"' EXIT

PKG_ROOT="$WORK_DIR/pkg"
DEBIAN_DIR="$PKG_ROOT/DEBIAN"
APP_DIR="$PKG_ROOT$INSTALL_DIR"
BIN_DIR="$PKG_ROOT/usr/bin"
DESKTOP_DIR="$PKG_ROOT/usr/share/applications"
ICON_DIR="$PKG_ROOT/usr/share/icons/hicolor/256x256/apps"

mkdir -p "$DEBIAN_DIR" "$APP_DIR" "$BIN_DIR" "$DESKTOP_DIR" "$ICON_DIR"

cp -R "$PUBLISH_DIR"/. "$APP_DIR/"
chmod +x "$APP_DIR/OpenClipboard.App"

cat > "$BIN_DIR/$APP_ID" <<'LAUNCHER'
#!/usr/bin/env bash
exec /opt/openclipboard/OpenClipboard.App "$@"
LAUNCHER
chmod +x "$BIN_DIR/$APP_ID"

cat > "$DESKTOP_DIR/$APP_ID.desktop" <<DESKTOP
[Desktop Entry]
Type=Application
Name=OpenClipboard
Comment=Clipboard history manager
Exec=$APP_ID
Icon=$APP_ID
Terminal=false
Categories=Utility;
StartupWMClass=OpenClipboard.App
DESKTOP

if [[ -f "$ICON_SOURCE" ]]; then
  cp "$ICON_SOURCE" "$ICON_DIR/$APP_ID.png"
else
  echo "Icon source not found, shipping without icon: $ICON_SOURCE" >&2
fi

# Runtime deps for self-contained .NET apps on common Debian/Ubuntu systems.
# Keep this conservative; users on minimal distros may need additional packages.
DEPENDS="libc6, libgcc-s1, libgssapi-krb5-2, libstdc++6, zlib1g, libssl3 | libssl1.1"

INSTALLED_SIZE=$(du -sk "$PKG_ROOT" | cut -f1)

cat > "$DEBIAN_DIR/control" <<CONTROL
Package: $APP_ID
Version: $VERSION
Section: utils
Priority: optional
Architecture: $ARCH
Maintainer: OpenClipboard Contributors <noreply@github.com>
Installed-Size: $INSTALLED_SIZE
Depends: $DEPENDS
Homepage: https://www.openclipboard.cc.cd
Description: Lightweight cross-platform clipboard history manager
 OpenClipboard records clipboard history (text, images, files) and provides
 a full-width bottom panel with search, favorites, and source app icons.
 Data is stored locally in SQLite.
CONTROL

mkdir -p "$(dirname "$OUTPUT_DEB")"
dpkg-deb --build --root-owner-group "$PKG_ROOT" "$OUTPUT_DEB"

echo "Created $OUTPUT_DEB"
