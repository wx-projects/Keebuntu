#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
VERSION="$(tr -d '[:space:]' < "$ROOT_DIR/VERSION")"
DIST_DIR="$ROOT_DIR/dist"
SOURCE_ARCHIVE="$DIST_DIR/keebuntu-status-notifier-$VERSION.tar.gz"
AUR_DIR="$ROOT_DIR/build/status-notifier/aur"
BUILD_DIR="$ROOT_DIR/build/status-notifier/arch-package"
PKG_NAME="keepass-plugin-keebuntu-status-notifier"
LOCAL_SOURCE="keebuntu-status-notifier-$VERSION.tar.gz"

[[ -s "$SOURCE_ARCHIVE" ]] || "$ROOT_DIR/build-status-notifier.sh"
"$ROOT_DIR/packaging/arch/build-aur-source.sh"

rm -rf "$BUILD_DIR"
mkdir -p "$BUILD_DIR"
cp "$AUR_DIR/PKGBUILD" "$BUILD_DIR/PKGBUILD"
cp "$SOURCE_ARCHIVE" "$BUILD_DIR/$LOCAL_SOURCE"

python3 - "$BUILD_DIR/PKGBUILD" <<'PY'
from pathlib import Path
import sys

path = Path(sys.argv[1])
text = path.read_text(encoding="utf-8")
remote = 'source=("https://github.com/wx-projects/Keebuntu/releases/download/v${pkgver}/keebuntu-status-notifier-${pkgver}.tar.gz")'
local = 'source=("keebuntu-status-notifier-${pkgver}.tar.gz")'
if remote not in text:
    raise SystemExit("Error: expected release source line was not found in PKGBUILD.")
path.write_text(text.replace(remote, local, 1), encoding="utf-8")
PY

grep -Fq 'source=("keebuntu-status-notifier-${pkgver}.tar.gz")' "$BUILD_DIR/PKGBUILD"

if command -v makepkg >/dev/null 2>&1 && [[ "$(id -u)" -ne 0 ]]; then
  (
    cd "$BUILD_DIR"
    makepkg --nodeps --noconfirm --cleanbuild
  )
elif command -v docker >/dev/null 2>&1; then
  docker run --rm \
    --user "$(id -u):$(id -g)" \
    --env HOME=/tmp/makepkg-home \
    --volume "$ROOT_DIR:/workspace" \
    --workdir /workspace/build/status-notifier/arch-package \
    archlinux:base-devel \
    bash -lc 'mkdir -p "$HOME" && makepkg --nodeps --noconfirm --cleanbuild'
else
  echo "Error: a non-root makepkg environment or Docker is required." >&2
  exit 1
fi

mapfile -t packages < <(
  find "$BUILD_DIR" -maxdepth 1 -type f \
    -name "$PKG_NAME-$VERSION-*.pkg.tar.zst" -print
)

if [[ "${#packages[@]}" -ne 1 ]]; then
  echo "Error: expected exactly one Arch package, found ${#packages[@]}." >&2
  printf '  %s\n' "${packages[@]:-}" >&2
  exit 1
fi

rm -f "$DIST_DIR/$PKG_NAME-$VERSION-"*.pkg.tar.zst
cp "${packages[0]}" "$DIST_DIR/"
test -s "$DIST_DIR/$(basename "${packages[0]}")"

echo "Created $DIST_DIR/$(basename "${packages[0]}")"
