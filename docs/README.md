# AetherXIV 2.0 Documentation

AetherXIV 2.0 includes two desktop applications:

- **AetherXIV Core** configures the database and manages the Map, World, Lobby,
  and Launcher Services processes.
- **AetherXIV Launcher** configures a user-owned Final Fantasy XIV 1.23b client,
  connects it to an AetherXIV server, and manages its compatibility runtime and
  optional Umbra framework.

Both applications are graphical. Normal use does not open or require a terminal.

## Install and run a release

Choose the guide for the computer that will run AetherXIV:

- [macOS setup](setup/MACOS.md)
- [Windows setup](setup/WINDOWS.md)
- [Linux setup](setup/LINUX.md)
- [SteamOS setup](setup/STEAMOS.md)

For a local all-in-one installation, begin with AetherXIV Core, complete the
database check, start the service stack, and then configure AetherXIV Launcher.

## Application guides

- [AetherXIV Core guide](AETHERXIV_CORE_GUIDE.md)
- [AetherXIV Launcher guide](LAUNCHER_GUIDE.md)
- [Database setup, updates, and recovery](DATABASE_SETUP_AND_MIGRATION.md)
- [Debugging and bug reporting](DEBUGGING_AND_BUG_REPORTING.md)
- [Umbra SDK and plugin development](UMBRA_SDK.md)

## Build from source

These guides are for contributors and release builders:

- [Build on macOS](build/MACOS.md)
- [Build on Windows](build/WINDOWS.md)
- [Build on Linux](build/LINUX.md)
- [Build on SteamOS](build/STEAMOS.md)
- [Complete build and runtime dependency matrix](BUILD_AND_RUNTIME_DEPENDENCIES.md)
- [Client research and gameplay restoration](development/CLIENT_REVERSE_ENGINEERING.md)

## Release information

- [AetherXIV 2.0 release notes](AETHERXIV_2.0_RELEASE_NOTES.md)

## Supported platforms

The primary supported targets are Apple silicon with macOS 14 or later,
Windows 11 x64, Ubuntu 22.04/24.04 x64, and the current SteamOS release in
Desktop Mode. Other Linux distributions may work but are considered best-effort.

## Client ownership

AetherXIV does not distribute the Final Fantasy XIV client, patches, or Square
Enix assets. Each user must supply a legally obtained Final Fantasy XIV 1.23b
client and any required client patch library.
