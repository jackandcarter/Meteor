# AetherXIV Setup on Windows

This guide supports Windows 11 x64. It installs a complete AetherXIV release
for a local all-in-one server and client.

## Before you begin

You need:

- the complete `Windows` release folder;
- [MariaDB Community Server](https://mariadb.org/download/);
- the [.NET 10 ASP.NET Core Runtime](https://dotnet.microsoft.com/download/dotnet/10.0)
  for the server processes;
- a user-owned Final Fantasy XIV 1.23b client.

The Core and Launcher GUIs and Windows client helpers are self-contained. Wine
is not used on Windows. No console window should appear when either GUI starts.

## Install the release

Extract the entire release to a stable writable folder, such as
`C:\AetherXIV`. Keep `core`, `launcher`, `servers`, and `Database` together.
Do not launch directly from the archive.

Only approve Windows SmartScreen or security prompts for a release you built
yourself or obtained from a trusted AetherXIV release source.

## Start the local server

1. Install MariaDB Server and allow it to run as a Windows service.
2. Open `core\app\AetherXIV.Core.App.exe`.
3. On **Config**, verify dependencies and the local database settings.
4. Select **Start Stack**.
5. Supply MariaDB administrator credentials if requested.
6. Wait for all four services to report **Running**.

If Windows Firewall asks about network access, allow only the network profiles
needed for your server. A local-only setup does not require public exposure.

## Configure the Launcher

1. Open `launcher\app\AetherXIV.Launcher.App.exe`.
2. Select **Localhost** on the **Server** tab and save.
3. Locate `ffxivboot.exe` or `ffxivgame.exe` on the **Client** tab.
4. Validate the client and configure FFXIV settings if required.
5. Leave the launch helper on **Automatic** unless troubleshooting requires a
   specific x86 or x64 helper.
6. Enable or update Umbra on the **Umbra** tab if desired.
7. Log in from **Home**.

The **Runtime** tab reports native Windows launch behavior; no Wine prefix is
required.

## Updating

Stop Core, keep the old release and a database backup, extract the new release
to a new folder, then start the new Core. Do not merge old generated files into
the new release folder.

## Troubleshooting

- AetherXIV's x86 native payloads statically link their compiler runtime; a
  missing DLL at this stage normally indicates an incomplete release folder.
- Confirm legacy game prerequisites separately if the original client fails
  before reaching AetherXIV services.
- Check Windows Firewall when another computer cannot reach advertised ports.
- Never upload Core settings without removing the database password.

See the [Launcher guide](../LAUNCHER_GUIDE.md), [Core guide](../AETHERXIV_CORE_GUIDE.md),
and [debugging guide](../DEBUGGING_AND_BUG_REPORTING.md).
