# AetherXIV Setup on Linux

Ubuntu 22.04 and 24.04 x64 are the primary supported Linux targets. Other x64
desktop distributions are best-effort.

## Before you begin

Install:

- [MariaDB Community Server](https://mariadb.org/download/);
- the [.NET 10 ASP.NET Core Runtime](https://dotnet.microsoft.com/download/dotnet/10.0);
- Avalonia desktop libraries documented by
  [Avalonia](https://docs.avaloniaui.net/docs/deployment/linux);
- working X11/XWayland and graphics-driver support;
- internet access so **Install Runtime** can retrieve the pinned portable Wine
  package;
- a user-owned Final Fantasy XIV 1.23b client.

On Ubuntu, the common Avalonia packages include `libx11-6`, `libice6`, `libsm6`,
and `libfontconfig1`. Use the package names appropriate to your distribution.

## Install the release

You can keep the extracted release in any permanent user-owned directory. Make
the two graphical apphosts executable after extracting the release:

```bash
chmod +x ./core/app/AetherXIV.Core.App
chmod +x ./launcher/app/AetherXIV.Launcher.App
```

Open `core/app/AetherXIV.Core.App` and `launcher/app/AetherXIV.Launcher.App`
directly from the extracted folders. The release does not ship `.desktop`
shortcuts because their executable paths become invalid when the release folder
is moved or installed under a different parent.

For a system-wide `/opt/aetherxiv` installation, copy the complete Linux release
there so `core`, `launcher`, `servers`, and `Database` remain together:

```bash
sudo install -d /opt/aetherxiv
sudo cp -a ./Linux/. /opt/aetherxiv/
sudo chmod +x /opt/aetherxiv/core/app/AetherXIV.Core.App
sudo chmod +x /opt/aetherxiv/launcher/app/AetherXIV.Launcher.App
```

## Start the local server

1. Start the MariaDB service.
2. Open `core/app/AetherXIV.Core.App` from the release folder.
3. Verify dependencies on **Config**.
4. Select **Start Stack** and provide MariaDB administrator credentials if
   requested.
5. Wait for all services to report **Running**.

## Configure the Launcher

1. Open `launcher/app/AetherXIV.Launcher.App` from the release folder.
2. Save the **Localhost** server preset.
3. Locate and validate the Final Fantasy XIV 1.23b client.
4. On **Runtime**, select **Install Runtime** if Wine is not detected. The
   Launcher downloads its pinned portable Linux x64 package, verifies it, and
   validates the executable, required host libraries, isolated prefix, and
   client helper. If a shared library is missing, install the named library
   with the distribution's graphical software manager and select **Validate
   Runtime** again.
5. Confirm graphics-driver support if Wine starts but the game does not render.
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

See the [Launcher guide](../LAUNCHER_GUIDE.md), [Core guide](../AETHERXIV_CORE_GUIDE.md),
and [debugging guide](../DEBUGGING_AND_BUG_REPORTING.md).
