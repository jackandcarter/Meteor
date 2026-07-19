# AetherXIV Setup on Linux

Ubuntu 22.04 and 24.04 x64 are the primary supported Linux targets. Other x64
desktop distributions are best-effort.

## Before you begin

Install:

- [MariaDB Community Server](https://mariadb.org/download/);
- the [.NET 10 ASP.NET Core Runtime](https://dotnet.microsoft.com/download/dotnet/10.0);
- Avalonia desktop libraries documented by
  [Avalonia](https://docs.avaloniaui.net/docs/deployment/linux);
- an approved Wine-compatible runtime and working 32-bit graphics/userspace;
- a user-owned Final Fantasy XIV 1.23b client.

On Ubuntu, the common Avalonia packages include `libx11-6`, `libice6`, `libsm6`,
and `libfontconfig1`. Use the package names appropriate to your distribution.

## Install the release

The supplied desktop entries target `/opt/aetherxiv`. Copy the complete Linux
release there so `core`, `launcher`, `servers`, `Database`, and `desktop` remain
together. Ensure the two GUI apphosts are executable:

```bash
sudo install -d /opt/aetherxiv
sudo cp -a ./Linux/. /opt/aetherxiv/
sudo chmod +x /opt/aetherxiv/core/app/AetherXIV.Core.App
sudo chmod +x /opt/aetherxiv/launcher/app/AetherXIV.Launcher.App
```

Install the desktop entries for all users if desired:

```bash
sudo install -m 0644 /opt/aetherxiv/desktop/org.aetherxiv.core.desktop /usr/share/applications/
sudo install -m 0644 /opt/aetherxiv/desktop/org.aetherxiv.launcher.desktop /usr/share/applications/
```

Both entries declare `Terminal=false`. Opening either application from the
desktop menu or a graphical file manager must not open a terminal window.

## Start the local server

1. Start the MariaDB service.
2. Open **AetherXIV Core** from the application menu.
3. Verify dependencies on **Config**.
4. Select **Start Stack** and provide MariaDB administrator credentials if
   requested.
5. Wait for all services to report **Running**.

## Configure the Launcher

1. Open **AetherXIV Launcher**.
2. Save the **Localhost** server preset.
3. Locate and validate the Final Fantasy XIV 1.23b client.
4. On **Runtime**, install or choose an approved Wine runtime and validate it.
5. Confirm 32-bit graphics support if Wine starts but the game does not render.
6. Enable Umbra if desired, then log in from **Home**.

An X11 session or XWayland compatibility layer is required.

## Updating

Stop the stack, back up the database, replace the complete `/opt/aetherxiv`
release with the new complete release, and start Core. Preserve user data under
the application-data directories; do not copy stale binaries into the new tree.

## Troubleshooting

- A GUI that exits immediately commonly indicates a missing X11, ICE, SM, or
  Fontconfig library.
- A game-only failure commonly indicates the Wine runtime, prefix, or 32-bit
  graphics stack rather than the Launcher GUI.
- Reinstall desktop entries after changing the installation root, or edit their
  `Exec`, `TryExec`, and `Icon` paths consistently.

See the [Launcher guide](../LAUNCHER_GUIDE.md), [Core guide](../AETHERXIV_CORE_GUIDE.md),
and [debugging guide](../DEBUGGING_AND_BUG_REPORTING.md).
