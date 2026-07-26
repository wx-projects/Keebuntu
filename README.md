# Keebuntu

Keebuntu contains Linux desktop integration plugins for KeePass 2.x running
under Mono. This fork maintains two tray implementations for different panel
protocols.

## Classic GTK Status Icon

Use the classic GTK2 plugin with i3bar and other trays that directly support
the XEmbed system tray protocol. It creates only the KeePass tray icon and does
not start a global StatusNotifier-to-XEmbed bridge, so unrelated application
icons are not re-rendered.

The maintained implementation:

- creates a direct GTK2/XEmbed tray icon;
- loads the icon from the KeePass/AppImage icon directory instead of relying on
  the host icon theme;
- disables the Mono WinForms tray icon only after the GTK icon is ready;
- retains KeePass' existing tray menu and restore behavior;
- no longer depends on ImageMagick or a StatusNotifierWatcher.

Do not install it together with the Status Notifier plugin or another KeePass
tray plugin.

## Status Notifier

The Status Notifier plugin exports a KDE StatusNotifierItem and D-Bus menu. It
is intended for KDE Plasma and panels that already provide a compatible
AppIndicator/StatusNotifier host.

The maintained implementation:

- detects KeePass at the standard Arch Linux and Debian/Ubuntu paths;
- removes the obsolete ImageMagick 6 runtime dependency;
- unregisters its D-Bus object during shutdown;
- disables the duplicate Mono WinForms tray icon only after successful startup;
- invokes KeePass' own **Tray / Untray** command so existing lock-on-minimize
  settings continue to apply.

Do not install this plugin together with the classic GTK tray icon or another
KeePass application-indicator plugin.

## Release packages

Each `vX.Y.Z` GitHub Release contains:

- `keebuntu-gtk-status-icon-X.Y.Z.tar.gz`: classic GTK2/XEmbed plugin for i3bar;
- `keebuntu-status-notifier-X.Y.Z.tar.gz`: generic StatusNotifier payload;
- `keepass2-plugin-status-notifier_X.Y.Z_all.deb`: Debian/Ubuntu package;
- `keepass-plugin-keebuntu-status-notifier-X.Y.Z-1-any.pkg.tar.zst`: directly
  installable Arch Linux StatusNotifier package;
- `keepass-plugin-keebuntu-status-notifier-aur-X.Y.Z.tar.gz`: AUR source package;
- `SHA256SUMS`: checksums for all release assets.

### Generic installation

Extract the required archive, enter its directory, then run as root:

```bash
./install.sh
```

For a custom KeePass application directory:

```bash
./install.sh --app-dir /absolute/path/to/keepass
```

### Debian/Ubuntu Status Notifier

```bash
sudo apt install ./keepass2-plugin-status-notifier_X.Y.Z_all.deb
```

### Arch Linux Status Notifier

```bash
sudo pacman -U ./keepass-plugin-keebuntu-status-notifier-X.Y.Z-1-any.pkg.tar.zst
```

## Building from source

Install KeePass, Mono/xbuild, GTK# 2, dbus-sharp and dbus-sharp-glib development
packages, then run:

```bash
./build-status-notifier.sh
./build-gtk-status-icon.sh
./packaging/debian/build-deb.sh
./packaging/arch/build-aur-source.sh
./packaging/arch/build-package.sh
```

To build against a non-standard KeePass installation:

```bash
KEEPASS_EXE=/absolute/path/to/KeePass.exe ./build-status-notifier.sh
KEEPASS_EXE=/absolute/path/to/KeePass.exe ./build-gtk-status-icon.sh
```

Generated files are written only to `build/` and `dist/`.

## License

Keebuntu is licensed under GPL-2.0-or-later. Release archives also contain the
MIT-licensed managed dbus-sharp assemblies; see `THIRD_PARTY_NOTICES.md`.
