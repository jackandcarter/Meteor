# Build AetherXIV on Linux

Ubuntu 22.04 and 24.04 x64 are the primary Linux build baselines. Other x64
distributions are best-effort.

## Requirements

- [.NET SDK 10](https://dotnet.microsoft.com/download/dotnet/10.0), exact SDK
  `10.0.203`
- [Python 3](https://www.python.org/downloads/)
- Bash, GNU core utilities, `find`, and SHA-256 tooling
- MinGW-w64 providing `i686-w64-mingw32-g++`
- Internet access for the initial NuGet restore

Use your distribution's packages from an official repository. On Ubuntu, locate
current package names through [Ubuntu Packages](https://packages.ubuntu.com/).
The common tool packages include Python 3 and MinGW-w64; install the .NET SDK
using [Microsoft's Linux instructions](https://learn.microsoft.com/dotnet/core/install/linux).

Confirm the required commands:

```bash
dotnet --list-sdks
python3 --version
i686-w64-mingw32-g++ --version
```

## Build

From the repository root:

```bash
./tools/Linux/build-aetherxiv.sh Release
```

For a development package:

```bash
./tools/Linux/build-aetherxiv.sh Debug
```

## Output

The full release is written to `bin/build/Release/Linux`:

```text
Linux/
├── core/app/AetherXIV.Core.App
├── launcher/app/AetherXIV.Launcher.App
├── servers/
└── Database/
```

The package intentionally omits path-dependent `.desktop` shortcuts. The
Launcher contains a self-contained Windows x64 managed helper plus the native
x86 Umbra payload used with the 32-bit Wine-hosted game. A 32-bit .NET runtime
is not required.

## Output reset warning

The build recreates the Linux release and removes unexpected top-level content
under `bin`. Do not store personal files there.

## Verification

The package layout is verified automatically. Run the complete solution and
Launcher tests with:

```bash
./tools/Development/verify-aetherxiv.sh
```

## Common failures

- Microsoft does not publish every SDK patch through every distribution feed;
  confirm that SDK `10.0.203` is actually selected.
- Some distributions name or split MinGW packages differently. The final test
  is whether `i686-w64-mingw32-g++` resolves on `PATH`.
- Runtime GUI libraries are not required merely to compile, but install the
  [Avalonia Linux dependencies](https://docs.avaloniaui.net/docs/deployment/linux)
  before testing the built applications.
