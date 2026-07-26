#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
VERSION="$(tr -d '[:space:]' < "$ROOT_DIR/VERSION")"
BUILD_DIR="$ROOT_DIR/build/gtk-status-icon"
PACKAGE_NAME="keebuntu-gtk-status-icon-$VERSION"
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

"$BUILD_TOOL" "$ROOT_DIR/GtkStatusIcon/GtkStatusIconPlugin.csproj" \
  /target:Clean \
  /property:Configuration=Release \
  "/property:KeePassExePath=$KEEPASS_EXE_PATH" \
  /verbosity:minimal

"$BUILD_TOOL" "$ROOT_DIR/GtkStatusIcon/GtkStatusIconPlugin.csproj" \
  /target:Build \
  /property:Configuration=Release \
  "/property:KeePassExePath=$KEEPASS_EXE_PATH" \
  /verbosity:minimal

PLUGIN_DLL="$ROOT_DIR/GtkStatusIcon/bin/Release/GtkStatusIcon.dll"
[[ -s "$PLUGIN_DLL" ]] || {
  echo "Error: GtkStatusIcon.dll was not produced." >&2
  exit 1
}

install -m 0644 "$PLUGIN_DLL" "$PLUGIN_DIR/GtkStatusIcon.dll"
cp -a "$ROOT_DIR/GtkStatusIcon/Resources/icons/." "$PACKAGE_ROOT/icons/"
install -m 0644 "$ROOT_DIR/README.md" "$PACKAGE_ROOT/README.md"
install -m 0644 "$ROOT_DIR/THIRD_PARTY_NOTICES.md" \
  "$PACKAGE_ROOT/THIRD_PARTY_NOTICES.md"
install -m 0644 "$ROOT_DIR/debian/copyright" "$PACKAGE_ROOT/COPYRIGHT"
install -m 0755 "$ROOT_DIR/packaging/install-gtk-status-icon.sh" \
  "$PACKAGE_ROOT/install.sh"

ARCHIVE="$DIST_DIR/$PACKAGE_NAME.tar.gz"
LIST_FILE="$BUILD_DIR/archive.list"
rm -f "$ARCHIVE"
tar -C "$BUILD_DIR/package" -czf "$ARCHIVE" "$PACKAGE_NAME"
tar -tzf "$ARCHIVE" > "$LIST_FILE"
grep -Fx "$PACKAGE_NAME/plugins/GtkStatusIcon.dll" "$LIST_FILE" >/dev/null
grep -Fx "$PACKAGE_NAME/install.sh" "$LIST_FILE" >/dev/null

echo "Created $ARCHIVE"
