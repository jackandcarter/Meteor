# AetherXIV Setup on SteamOS

SteamOS uses the Linux release ABI and is supported in Steam Deck Desktop Mode.
The current SteamOS release is the primary target; modified or older images are
best-effort.

## Before you begin

You need the complete `SteamOS` release, a user-owned Final Fantasy XIV 1.23b
client, MariaDB, the .NET 10 ASP.NET Core Runtime, internet access for the
Launcher-managed Wine download, and the Linux desktop libraries listed in the
[dependency matrix](../BUILD_AND_RUNTIME_DEPENDENCIES.md). A separate Wine
installation is not required.

SteamOS has a read-only base image. System updates can replace packages or
changes made outside persistent storage. Plan where MariaDB data, runtimes,
prefixes, and the AetherXIV release will live before configuring the server.

## Install in Desktop Mode

1. Enter Desktop Mode.
2. Extract the entire SteamOS release to persistent storage.
3. Keep `core`, `launcher`, `servers`, `Database`, and `desktop` together.
4. Mark the Core and Launcher apphosts executable.
5. Adjust the supplied desktop-entry `Exec`, `TryExec`, and `Icon` paths if you
   do not install under `/opt/aetherxiv`.
6. Add the desktop entries to the application menu if desired.

The entries use `Terminal=false`; the applications are graphical and should
not open a terminal.

## Local setup

1. Start MariaDB and open **AetherXIV Core**.
2. Verify dependencies and complete database setup.
3. Start the stack and wait for all services.
4. Open **AetherXIV Launcher**, save **Localhost**, and locate the 1.23b client.
5. Select **Install Runtime**. The portable Linux x64 package is verified and
   installed in persistent Launcher application data, without modifying the
   read-only SteamOS base image. Validation lists any missing host library;
   install it in a persistent SteamOS/Arch environment and validate again.
6. Enable Umbra if desired, then log in.

Running the server and client together is convenient for development but may
be resource intensive on a Steam Deck. A remote AetherXIV server can be selected
from the Launcher's **Server** tab instead.

If the persistent Wine executable is not available on the desktop session's
`PATH`, choose **Custom Runtime** and enter its absolute executable path. Do not
disable SteamOS read-only protection merely to make automatic detection work.

## After SteamOS updates

Recheck the .NET runtime, MariaDB service, graphics support, desktop-entry
paths, and executable bits. Launcher-managed data and prefixes should remain in
writable user storage.

See the [Linux setup guide](LINUX.md) for the shared runtime process and the
[debugging guide](../DEBUGGING_AND_BUG_REPORTING.md) for log locations.
