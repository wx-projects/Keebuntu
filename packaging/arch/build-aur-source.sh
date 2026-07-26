#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
VERSION="$(tr -d '[:space:]' < "$ROOT_DIR/VERSION")"
DIST_DIR="$ROOT_DIR/dist"
SOURCE_ARCHIVE="$DIST_DIR/keebuntu-status-notifier-$VERSION.tar.gz"
AUR_DIR="$ROOT_DIR/build/status-notifier/aur"
AUR_ARCHIVE="$DIST_DIR/keepass-plugin-keebuntu-status-notifier-aur-$VERSION.tar.gz"

[[ -s "$SOURCE_ARCHIVE" ]] || "$ROOT_DIR/build-status-notifier.sh"

SHA256="$(sha256sum "$SOURCE_ARCHIVE" | awk '{print $1}')"
rm -rf "$AUR_DIR"
mkdir -p "$AUR_DIR"

sed \
  -e "s/@VERSION@/$VERSION/g" \
  -e "s/@SHA256@/$SHA256/g" \
  "$ROOT_DIR/packaging/arch/PKGBUILD.in" > "$AUR_DIR/PKGBUILD"

cat > "$AUR_DIR/.SRCINFO" <<EOF_SRCINFO
pkgbase = keepass-plugin-keebuntu-status-notifier
	pkgdesc = StatusNotifierItem tray integration for KeePass 2.x on Linux
	pkgver = $VERSION
	pkgrel = 1
	url = https://github.com/wx-projects/Keebuntu
	arch = any
	license = GPL-2.0-or-later
	license = MIT
	depends = keepass
	depends = mono
	depends = gtk-sharp-2
	depends = dbus-glib
	provides = keepass-plugin-status-notifier
	conflicts = keebuntu-git
	source = https://github.com/wx-projects/Keebuntu/releases/download/v$VERSION/keebuntu-status-notifier-$VERSION.tar.gz
	sha256sums = $SHA256

pkgname = keepass-plugin-keebuntu-status-notifier
EOF_SRCINFO

grep -Fq "$SHA256" "$AUR_DIR/PKGBUILD"
grep -Fq "$SHA256" "$AUR_DIR/.SRCINFO"
rm -f "$AUR_ARCHIVE"
tar -C "$AUR_DIR" -czf "$AUR_ARCHIVE" PKGBUILD .SRCINFO
tar -tzf "$AUR_ARCHIVE" >/dev/null

echo "Created $AUR_ARCHIVE"
