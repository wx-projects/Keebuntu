#!/usr/bin/env bash
set -euo pipefail

SOURCE_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
APP_DIR="${KEEPASS_APP_DIR:-}"
DESTDIR="${DESTDIR:-}"

if [[ "${1:-}" == "--app-dir" ]]; then
  [[ -n "${2:-}" ]] || {
    echo "Error: --app-dir requires an absolute KeePass application directory." >&2
    exit 1
  }
  APP_DIR="$2"
fi

if [[ -z "$APP_DIR" ]]; then
  if [[ -f /usr/share/keepass/KeePass.exe ]]; then
    APP_DIR=/usr/share/keepass
  elif [[ -f /usr/lib/keepass2/KeePass.exe ]]; then
    APP_DIR=/usr/lib/keepass2
  else
    echo "Error: KeePass was not found. Set KEEPASS_APP_DIR or use --app-dir." >&2
    exit 1
  fi
fi

[[ "$APP_DIR" == /* ]] || {
  echo "Error: KeePass application directory must be an absolute path." >&2
  exit 1
}

PLUGIN_DEST="$DESTDIR$APP_DIR/Plugins/keebuntu"
ICON_DEST="$DESTDIR/usr/share/icons"

install -d "$PLUGIN_DEST" "$ICON_DEST"
install -m 0644 "$SOURCE_DIR/plugins/"*.dll "$PLUGIN_DEST/"
install -m 0644 "$SOURCE_DIR/plugins/"*.dll.config "$PLUGIN_DEST/"
cp -a "$SOURCE_DIR/icons/." "$ICON_DEST/"

test -f "$PLUGIN_DEST/dbus-sharp-glib.dll.config"

echo "Installed Keebuntu Status Notifier into $PLUGIN_DEST"
