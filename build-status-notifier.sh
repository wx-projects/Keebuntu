#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
VERSION="$(tr -d '[:space:]' < "$ROOT_DIR/VERSION")"
BUILD_DIR="$ROOT_DIR/build/status-notifier"
PACKAGE_NAME="keebuntu-status-notifier-$VERSION"
PACKAGE_ROOT="$BUILD_DIR/package/$PACKAGE_NAME"
PLUGIN_DIR="$PACKAGE_ROOT/plugins"
DIST_DIR="$ROOT_DIR/dist"

find_keepass_exe() {
  if [[ -n "${KEEPASS_EXE:-}" ]]; then
    printf '%s\n' "$KEEPASS_EXE"
    return
  fi

  local candidate
  for candidate in \
    /usr/share/keepass/KeePass.exe \
    /usr/lib/keepass2/KeePass.exe; do
    if [[ -f "$candidate" ]]; then
      printf '%s\n' "$candidate"
      return
    fi
  done

  return 1
}

find_managed_assembly() {
  local filename="$1"
  local result

  result="$(find /usr/lib/mono /usr/lib/cli -type f -name "$filename" \
    -print -quit 2>/dev/null || true)"
  [[ -n "$result" ]] || {
    echo "Error: $filename was not found in the installed Mono libraries." >&2
    exit 1
  }
  printf '%s\n' "$result"
}

KEEPASS_EXE_PATH="$(find_keepass_exe || true)"
[[ -n "$KEEPASS_EXE_PATH" && -f "$KEEPASS_EXE_PATH" ]] || {
  echo "Error: KeePass.exe was not found. Set KEEPASS_EXE to its absolute path." >&2
  exit 1
}

if command -v msbuild >/dev/null 2>&1; then
  BUILD_TOOL="msbuild"
elif command -v xbuild >/dev/null 2>&1; then
  BUILD_TOOL="xbuild"
else
  echo "Error: neither msbuild nor xbuild is installed." >&2
  exit 1
fi

rm -rf "$BUILD_DIR"
mkdir -p "$PLUGIN_DIR" "$PACKAGE_ROOT/icons" "$DIST_DIR"

"$BUILD_TOOL" "$ROOT_DIR/DBus/DBus.csproj" \
  /target:Clean \
  /property:Configuration=Release \
  /verbosity:minimal

"$BUILD_TOOL" "$ROOT_DIR/StatusNotifierPlugin/StatusNotifierPlugin.csproj" \
  /target:Clean \
  /property:Configuration=Release \
  "/property:KeePassExePath=$KEEPASS_EXE_PATH" \
  /verbosity:minimal

"$BUILD_TOOL" "$ROOT_DIR/StatusNotifierPlugin/StatusNotifierPlugin.csproj" \
  /target:Build \
  /property:Configuration=Release \
  "/property:KeePassExePath=$KEEPASS_EXE_PATH" \
  /verbosity:minimal

DBUS_DLL="$ROOT_DIR/DBus/bin/Release/DBus.dll"
STATUS_NOTIFIER_DLL="$ROOT_DIR/StatusNotifierPlugin/bin/Release/KeebuntuStatusNotifier.dll"
DBUS_SHARP_DLL="$(find_managed_assembly dbus-sharp.dll)"
DBUS_SHARP_GLIB_DLL="$(find_managed_assembly dbus-sharp-glib.dll)"
DBUS_SHARP_GLIB_CONFIG="$ROOT_DIR/packaging/dbus-sharp-glib.dll.config"

for file in \
  "$DBUS_DLL" \
  "$STATUS_NOTIFIER_DLL" \
  "$DBUS_SHARP_DLL" \
  "$DBUS_SHARP_GLIB_DLL" \
  "$DBUS_SHARP_GLIB_CONFIG"; do
  [[ -s "$file" ]] || {
    echo "Error: expected build output is missing: $file" >&2
    exit 1
  }
done

grep -Fq 'dll="libglib-2.0-0.dll"' "$DBUS_SHARP_GLIB_CONFIG"
grep -Fq 'target="libglib-2.0.so.0"' "$DBUS_SHARP_GLIB_CONFIG"

install -m 0644 "$DBUS_DLL" "$PLUGIN_DIR/DBus.dll"
install -m 0644 "$STATUS_NOTIFIER_DLL" "$PLUGIN_DIR/KeebuntuStatusNotifier.dll"
install -m 0644 "$DBUS_SHARP_DLL" "$PLUGIN_DIR/dbus-sharp.dll"
install -m 0644 "$DBUS_SHARP_GLIB_DLL" "$PLUGIN_DIR/dbus-sharp-glib.dll"
install -m 0644 "$DBUS_SHARP_GLIB_CONFIG" "$PLUGIN_DIR/dbus-sharp-glib.dll.config"
cp -a "$ROOT_DIR/StatusNotifierPlugin/Resources/icons/." "$PACKAGE_ROOT/icons/"
install -m 0644 "$ROOT_DIR/README.md" "$PACKAGE_ROOT/README.md"
install -m 0644 "$ROOT_DIR/THIRD_PARTY_NOTICES.md" "$PACKAGE_ROOT/THIRD_PARTY_NOTICES.md"
install -m 0644 "$ROOT_DIR/debian/copyright" "$PACKAGE_ROOT/COPYRIGHT"
install -m 0755 "$ROOT_DIR/packaging/install.sh" "$PACKAGE_ROOT/install.sh"

rm -f "$DIST_DIR/$PACKAGE_NAME.tar.gz"
tar -C "$BUILD_DIR/package" -czf "$DIST_DIR/$PACKAGE_NAME.tar.gz" "$PACKAGE_NAME"
tar -tzf "$DIST_DIR/$PACKAGE_NAME.tar.gz" >/dev/null
tar -tzf "$DIST_DIR/$PACKAGE_NAME.tar.gz" |
  grep -Fx "$PACKAGE_NAME/plugins/dbus-sharp-glib.dll.config" >/dev/null

echo "Created $DIST_DIR/$PACKAGE_NAME.tar.gz"
