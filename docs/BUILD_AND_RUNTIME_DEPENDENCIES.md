# AetherXIV 2.0 Build and Runtime Dependencies

This document separates what a release builder needs from what a player or
server administrator needs. AetherXIV Core and AetherXIV Launcher are graphical,
self-contained applications in release packages. Launching either application
does not require a terminal or a separate desktop .NET installation.

## Supported release targets

| Platform | Primary target | Build entry point | Packaged GUI |
|---|---|---|---|
| macOS | Apple silicon, macOS 14 or later | `tools/MacOS/build-aetherxiv.sh` | `AetherXIV Core.app`, `AetherXIV Launcher.app` |
| Windows | Windows 11 x64 | `tools/Windows/build-aetherxiv.ps1` | `AetherXIV.Core.App.exe`, `AetherXIV.Launcher.App.exe` |
| Linux | x64 desktop Linux | `tools/Linux/build-aetherxiv.sh` | Native GUI apphosts plus `.desktop` entries |
| SteamOS | Steam Deck Desktop Mode | `tools/SteamOS/build-aetherxiv.sh` | Linux GUI apphosts plus `.desktop` entries |

Linux and SteamOS desktop entries target an installation rooted at
`/opt/aetherxiv`. Both entries declare `Terminal=false`. The native GUI
executables can also be opened directly from a graphical file manager without
starting a separate terminal.

## Requirements for every build host

- .NET SDK 10.0.203. The version is pinned in `global.json`.
- Python 3 for source-manifest validation and database packaging.
- Internet access for the initial NuGet restore, unless all packages are
  already available in a configured offline cache.
- Enough disk space for the .NET publish output, two self-contained Windows
  helper runtimes, and the native Umbra payload.
- A compiler that provides the 32-bit Windows C++ Umbra toolchain described
  below.

The build scripts publish the Core and Launcher GUIs self-contained. Server
hosts are intentionally framework-dependent to keep the server package smaller.
The four existing platform build commands perform an early prerequisite check
and report every missing tool together. They do not install SDKs, compilers, or
package managers. GitHub Actions provisions those tools before invoking the
same build commands used by developers.

## macOS build host

Required:

- macOS 14 or later on Apple silicon for the default `osx-arm64` release.
- .NET SDK 10.0.203.
- Python 3.
- Bash and standard macOS command-line utilities.
- MinGW-w64 with the exact command `i686-w64-mingw32-g++` available on `PATH`.

The MinGW cross-compiler builds the Windows x86 native injector and Umbra
bootstrap used inside the legacy game process. Xcode is not used by the current
release script.

## Windows build host

Required:

- Windows 11 x64.
- PowerShell 5.1 or PowerShell 7.
- .NET SDK 10.0.203.
- Python 3 (`python3`, `python`, or `py -3`).
- One native build path:
  - Visual Studio Build Tools with MSBuild, the MSVC v143 C++ toolset, and a
    Windows SDK; or
  - MinGW-w64 with `i686-w64-mingw32-g++` available on `PATH`.

The Windows script prefers MSBuild when it is installed and falls back to the
MinGW compiler.

## Linux build host

Required:

- A supported x64 .NET 10 distribution. Ubuntu 22.04 or 24.04 is the primary
  release-build baseline.
- .NET SDK 10.0.203.
- Python 3.
- Bash, GNU core utilities, `find`, and `sha256sum`-compatible tooling.
- A MinGW-w64 installation that provides `i686-w64-mingw32-g++`.

## SteamOS build host

SteamOS uses the Linux build recipe and ABI. Build in Desktop Mode or another
writable development environment with:

- .NET SDK 10.0.203.
- Python 3.
- Bash and standard Linux utilities.
- `i686-w64-mingw32-g++`.

SteamOS system updates can replace changes made to its read-only base image.
Keep SDKs and build caches in writable user or development storage.

## Core runtime dependencies

The AetherXIV Core GUI itself is self-contained. Running the services it manages
requires:

- .NET 10 ASP.NET Core Runtime for the host platform. This includes the base
  .NET runtime needed by the framework-dependent Map, World, Lobby, and Launcher
  Services hosts.
- MariaDB Server and a MariaDB/MySQL-compatible command-line client for initial
  setup, migrations, backups, and restores.
- Write access to the selected configuration, log, and database backup folders.
- Local firewall permission for the configured service ports.

Default service ports are:

- Launcher Services HTTP: `8080`
- Lobby: `54994`
- World: `54992`
- Map: `1989`

Only expose ports required by the intended network layout. Database port `3306`
should normally remain local or restricted to trusted hosts.

## Launcher runtime dependencies

The AetherXIV Launcher GUI and its Windows client helpers are self-contained.
All platforms additionally require a user-owned Final Fantasy XIV 1.23b client
installation.

### Windows

- No Wine runtime is needed.
- The game client must be able to run as a 32-bit Windows application.
- AetherXIV's native x86 payloads statically link their compiler runtime.
- Any legacy DirectX prerequisites required by the original client remain game
  prerequisites, not Launcher dependencies.

### macOS

- The checksum-pinned managed Wine runtime installed by the Launcher, or a
  compatible local macOS runtime detected and validated by the Launcher.
- Rosetta 2 when the selected compatibility runtime contains Intel-only macOS
  components. Runtime validation triggers Apple's normal prompt and waits for
  completion when Rosetta is absent.
- GStreamer is optional for launching; without it some Wine-hosted movies or
  media may not play. The Launcher does not install its unsigned upstream
  package automatically.
- Permission for the Launcher and compatibility runtime to access the client,
  prefix, plugin, cache, and log directories.

### Linux and SteamOS

- A graphical X11 session or XWayland compatibility layer.
- Avalonia's native desktop libraries: X11, ICE, SM, and Fontconfig.
- The checksum-pinned portable Wine runtime installed by the Launcher, or a
  compatible local runtime detected and validated by the Launcher.
- Working host graphics drivers for the legacy x86 game client. The selected
  amd64-wow64 Wine build does not require 32-bit Linux libraries.

Before prefix creation, the Launcher checks the selected Linux Wine executable
and its principal X11, audio, GStreamer, and Vulkan drivers with `ldd`. A missing
library blocks validation with its exact soname and platform-family guidance.

Distribution package names vary. On Debian/Ubuntu, the Avalonia libraries are
commonly provided by `libx11-6`, `libice6`, `libsm6`, and `libfontconfig1`.

## What is included in a release

- Self-contained AetherXIV Core and Launcher GUIs.
- Self-contained Windows x64 and x86 client-launch helpers.
- Umbra's Windows x86 native bootstrap, injector, managed framework, assets,
  and plugin API payload.
- Framework-dependent Map, World, Lobby, and Launcher Services hosts.
- Canonical database baseline, migrations, setup scripts, hashes, and manifest.
- Map scripts, static actor data, and navigation mesh assets.

## Upstream platform references

Use official sources when installing build or runtime dependencies:

- [.NET 10 SDK and runtimes](https://dotnet.microsoft.com/download/dotnet/10.0)
- [.NET 10 supported operating systems](https://github.com/dotnet/core/blob/main/release-notes/10.0/supported-os.md)
- [Microsoft .NET installation documentation](https://learn.microsoft.com/dotnet/core/install/)
- [Python 3 downloads](https://www.python.org/downloads/)
- [MariaDB Community Server downloads](https://mariadb.org/download/)
- [MariaDB installation guides](https://mariadb.com/docs/server/mariadb-quickstart-guides/installing-mariadb-server-guide)
- [Avalonia Linux deployment dependencies](https://docs.avaloniaui.net/docs/deployment/linux)

Platform-specific build sources:

- [Homebrew for macOS](https://brew.sh/)
- [Homebrew MinGW-w64 formula](https://formulae.brew.sh/formula/mingw-w64)
- [Visual Studio and Build Tools downloads](https://visualstudio.microsoft.com/downloads/)
- [MSYS2](https://www.msys2.org/) as an alternative Windows MinGW-w64 environment
- [Ubuntu package search](https://packages.ubuntu.com/) for distribution-provided build and desktop libraries
- [WineHQ downloads](https://www.winehq.org/download) for advanced custom-runtime users
- [Gcenx macOS Wine builds](https://github.com/Gcenx/macOS_Wine_builds), the
  source of the pinned macOS Wine 11.0_1 archive
- [Kron4ek Wine builds](https://github.com/Kron4ek/Wine-Builds), the source of
  the pinned Linux Wine 11.0 amd64-wow64 archive

The Launcher ships the pinned package definitions and checksums, not copies of
the upstream Wine archives. **Install Runtime** downloads the selected archive
directly from its upstream release and refuses a byte-length or SHA-256
mismatch.

Do not substitute a newer major .NET SDK without also updating `global.json` and
validating the complete build. AetherXIV currently pins SDK `10.0.203`.
