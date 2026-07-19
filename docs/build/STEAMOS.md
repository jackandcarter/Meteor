# Build AetherXIV on SteamOS

SteamOS uses the Linux ABI and build implementation but writes an independent
SteamOS release directory.

## Requirements

- Current SteamOS in Desktop Mode or another writable development environment
- [.NET SDK 10](https://dotnet.microsoft.com/download/dotnet/10.0), exact SDK
  `10.0.203`
- [Python 3](https://www.python.org/downloads/)
- Bash and standard Linux utilities
- MinGW-w64 providing `i686-w64-mingw32-g++`
- Sufficient persistent storage for SDKs, NuGet caches, and release output

Because the SteamOS base image is read-only and system updates can replace local
changes, prefer a persistent development container or user-owned tool location.

## Build

From the repository root:

```bash
./tools/SteamOS/build-aetherxiv.sh Release
```

The entry point sets the platform name to `SteamOS` and delegates to the Linux
build recipe, preventing the two package formats from drifting.

## Output

The complete release is written to:

```text
bin/build/Release/SteamOS/
```

It includes Core, Launcher, all services, database tools, desktop entries and
their icon assets, Windows helpers, and Umbra.

## Output reset and verification

The build recreates the selected release and cleans repository-owned `bin`
content. The final layout is verified automatically. Run the full test suite
when preparing a release:

```bash
./tools/Development/verify-aetherxiv.sh
```

If maintaining the toolchain directly on SteamOS becomes unreliable, build the
SteamOS target from a compatible x64 Linux development host and test the final
package on the target SteamOS version.
