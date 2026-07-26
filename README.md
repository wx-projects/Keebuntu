# Keebuntu

Keebuntu contains Linux desktop integration plugins for KeePass 2.x running
under Mono. This fork currently maintains and releases the **Status Notifier**
plugin. The legacy GTK tray icon and Unity launcher sources remain in the
repository but are not included in current release packages.

## Status Notifier

The plugin exports a KDE StatusNotifierItem and D-Bus menu. It is intended for
KDE Plasma and also works with desktop panels that provide a compatible
AppIndicator/StatusNotifier host.

The maintained implementation:

- detects KeePass at the standard Arch Linux and Debian/Ubuntu paths;
- removes the obsolete ImageMagick 6 runtime dependency;
- unregisters its D-Bus object during shutdown;
- disables the duplicate Mono WinForms tray icon only after successful startup;
- invokes KeePass' own **Tray / Untray** command so existing lock-on-minimize
  settings continue to apply.

Do not install this plugin together with Keebuntu's classic tray icon or another
KeePass application-indicator plugin.

## Release packages

Each `vX.Y.Z` GitHub Release contains:

- `keebuntu-status-notifier-X.Y.Z.tar.gz`: generic Linux payload and installer;
- `keepass2-plugin-status-notifier_X.Y.Z_all.deb`: Debian/Ubuntu package;
- `keepass-plugin-keebuntu-status-notifier-aur-X.Y.Z.tar.gz`: `PKGBUILD` and
  `.SRCINFO` ready for AUR publication;
- `SHA256SUMS`: checksums for all release assets.

### Generic installation

Extract the release archive, enter its directory, then run as root:

```bash
./install.sh
```

The installer detects `/usr/share/keepass` and `/usr/lib/keepass2`. For a custom
KeePass application directory:

```bash
./install.sh --app-dir /absolute/path/to/keepass
```

### Debian/Ubuntu

```bash
sudo apt install ./keepass2-plugin-status-notifier_X.Y.Z_all.deb
```

### Arch Linux

Extract the AUR source archive and build it with `makepkg`. The package depends
on `keepass`, `mono`, `gtk-sharp-2` and `dbus-glib`.

## Building from source

Install KeePass, Mono/xbuild, GTK# 2, dbus-sharp and dbus-sharp-glib development
packages, then run:

```bash
./build-status-notifier.sh
./packaging/debian/build-deb.sh
./packaging/arch/build-aur-source.sh
```

To build against a non-standard KeePass installation:

```bash
KEEPASS_EXE=/absolute/path/to/KeePass.exe ./build-status-notifier.sh
```

Generated files are written only to `build/` and `dist/`.

## Legacy plugins

The following upstream components remain available as unmaintained source code:

- `GtkStatusIcon`: classic GTK status icon for older Cinnamon/MATE setups;
- `UnityLauncherPlugin`: Unity launcher quicklist integration.

They are intentionally excluded from the maintained Status Notifier release.

## License

Keebuntu is licensed under GPL-2.0-or-later. Release archives also contain the
MIT-licensed managed dbus-sharp assemblies; see `THIRD_PARTY_NOTICES.md`.
