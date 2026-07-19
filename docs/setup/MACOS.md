# AetherXIV Setup on macOS

This guide supports Apple silicon Macs running macOS 14 or later. It installs a
complete AetherXIV release for a local all-in-one server and client.

## Before you begin

You need:

- the complete `MacOS` release folder;
- [MariaDB Community Server](https://mariadb.org/download/);
- the [.NET 10 ASP.NET Core Runtime](https://dotnet.microsoft.com/download/dotnet/10.0)
  for the server processes;
- a user-owned Final Fantasy XIV 1.23b client;
- internet access so **Install Runtime** can retrieve the pinned macOS Wine
  package;
- [Rosetta 2](https://support.apple.com/en-us/102527), which macOS offers to
  install during Launcher validation when it is absent.

The Core and Launcher applications themselves are self-contained. They open as
normal macOS applications without Terminal windows.

## Install the release

Keep the complete release together. `AetherXIV Core.app`, `AetherXIV
Launcher.app`, `Database`, and `build-manifest.txt` are one release unit.
Moving only the Core application can prevent it from finding the packaged
database installer.

You may place the entire release folder under `/Applications`, or keep it in a
writable folder owned by your account. Do not run it from inside an archive.

Current development builds may be unsigned and unnotarized. Only open an
unsigned build you produced yourself or obtained from a trusted AetherXIV
release source. Prefer signed releases when they are available.

## Start the local server

1. Install and start MariaDB.
2. Open **AetherXIV Core.app**.
3. Open **Config** and confirm the default local endpoints and database values.
4. Select **Verify Dependencies**.
5. Select **Start Stack**.
6. Enter the MariaDB administrator credentials if Core requests them.
7. Wait for Map, World, Lobby, and Launcher Services to show **Running**.

Administrator credentials are not saved. Core does save its application
database password; see the [database guide](../DATABASE_SETUP_AND_MIGRATION.md).

## Configure the Launcher

1. Open **AetherXIV Launcher.app**.
2. On **Server**, select **Localhost** and choose **Save Settings**.
3. On **Client**, browse to `ffxivboot.exe` for an unpatched client or
   `ffxivgame.exe` for a patched client.
4. Select **Validate Client**. The client must report the supported 1.23b state.
5. On **Runtime**, select **Install Runtime** if Wine is not detected. The
   Launcher downloads and verifies its pinned macOS package, installs it into
   Launcher storage, and validates it. Apple silicon requires Rosetta. If it is
   absent, complete Apple's installation prompt; the Launcher waits and then
   continues automatically.
6. On **Umbra**, enable the framework if desired.
7. Return to **Home**, enter the account details, and select **Log In & Play**.

The first runtime or FFXIV Settings operation may take several seconds while
the Wine prefix is checked or prepared. GStreamer is optional; the Launcher
warns when it is absent because some movies or media may not play, but it does
not install the upstream unsigned package automatically.

## Network access

The local preset uses loopback addresses and requires no router changes. For a
remote server, allow only the configured Launcher, Lobby, World, and Map ports.
Do not expose MariaDB port `3306` publicly.

## Updating

Stop the stack, preserve the complete old release and database backup, extract
the new release into a new folder, and open the new Core. Core will validate the
database contract and request permission before a compatibility migration.

## Troubleshooting

- If Core cannot find the database package, restore the original release-folder
  layout.
- If a runtime is not listed, choose **Scan Runtimes** or configure it as a
  custom runtime only if you understand its Wine command and prefix.
- If the client cannot access files, confirm macOS has granted the application
  access to the client and runtime folders.
- Use the Launcher **Launch Log** and Core **Logs** tabs before filing a report.

See the [Launcher guide](../LAUNCHER_GUIDE.md), [Core guide](../AETHERXIV_CORE_GUIDE.md),
and [debugging guide](../DEBUGGING_AND_BUG_REPORTING.md) for more detail.
