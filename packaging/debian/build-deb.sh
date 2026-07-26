#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
VERSION="$(tr -d '[:space:]' < "$ROOT_DIR/VERSION")"
BUILD_DIR="$ROOT_DIR/build/status-notifier"
PACKAGE_NAME="keebuntu-status-notifier-$VERSION"
PAYLOAD_DIR="$BUILD_DIR/package/$PACKAGE_NAME"
DEB_ROOT="$BUILD_DIR/debian-root"
DIST_DIR="$ROOT_DIR/dist"
OUTPUT="$DIST_DIR/keepass2-plugin-status-notifier_${VERSION}_all.deb"

[[ -d "$PAYLOAD_DIR/plugins" ]] || "$ROOT_DIR/build-status-notifier.sh"

rm -rf "$DEB_ROOT"
mkdir -p \
  "$DEB_ROOT/DEBIAN" \
  "$DEB_ROOT/usr/lib/keepass2/Plugins/keebuntu" \
  "$DEB_ROOT/usr/share/icons" \
  "$DEB_ROOT/usr/share/doc/keepass2-plugin-status-notifier"

install -m 0644 "$PAYLOAD_DIR/plugins/"*.dll \
  "$DEB_ROOT/usr/lib/keepass2/Plugins/keebuntu/"
cp -a "$PAYLOAD_DIR/icons/." "$DEB_ROOT/usr/share/icons/"
install -m 0644 "$PAYLOAD_DIR/COPYRIGHT" \
  "$DEB_ROOT/usr/share/doc/keepass2-plugin-status-notifier/copyright"
install -m 0644 "$PAYLOAD_DIR/THIRD_PARTY_NOTICES.md" \
  "$DEB_ROOT/usr/share/doc/keepass2-plugin-status-notifier/THIRD_PARTY_NOTICES.md"

cat > "$DEB_ROOT/DEBIAN/control" <<EOF_CONTROL
Package: keepass2-plugin-status-notifier
Version: $VERSION
Section: utils
Priority: optional
Architecture: all
Maintainer: wx-projects
Depends: keepass2, libgtk2.0-cil, libdbus-glib-1-2
Conflicts: keepass2-plugin-tray-icon, keepass2-plugin-application-indicator
Description: StatusNotifierItem tray integration for KeePass 2.x on Linux
 Provides a KDE StatusNotifierItem and D-Bus menu for KeePass 2.x running
 under Mono. The package also works with compatible AppIndicator hosts.
EOF_CONTROL

rm -f "$OUTPUT"
dpkg-deb --root-owner-group --build "$DEB_ROOT" "$OUTPUT"
dpkg-deb --info "$OUTPUT" >/dev/null
dpkg-deb --contents "$OUTPUT" >/dev/null

echo "Created $OUTPUT"
