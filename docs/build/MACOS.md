# Build AetherXIV on macOS

This build produces the complete Apple-silicon macOS release, including both
GUI applications, all server services, database tooling, Windows client helpers,
and Umbra's x86 Windows payload.

## Requirements

- Apple silicon with macOS 14 or later
- [.NET SDK 10](https://dotnet.microsoft.com/download/dotnet/10.0), exact SDK
  `10.0.203` as pinned by `global.json`
- [Python 3](https://www.python.org/downloads/)
- [Homebrew](https://brew.sh/)
- [MinGW-w64](https://formulae.brew.sh/formula/mingw-w64), providing the exact
  command `i686-w64-mingw32-g++`
- Internet access for the initial NuGet restore

The current script does not require Xcode.

Example dependency installation after Homebrew is installed:

```bash
brew install mingw-w64 python
```

Install .NET from Microsoft's official download and confirm the pinned SDK:

```bash
dotnet --list-sdks
python3 --version
i686-w64-mingw32-g++ --version
```

## Build

From the repository root:

```bash
./tools/MacOS/build-aetherxiv.sh Release
```

Use `Debug` instead of `Release` for a symbol-bearing development package:

```bash
./tools/MacOS/build-aetherxiv.sh Debug
```

The default runtime identifier is `osx-arm64`. Advanced builders may override
the documented `AETHERXIV_*` environment variables used at the top of the build
script, but every changed target must be verified independently.

## Output

The complete release is written to:

```text
bin/build/Release/MacOS/
├── AetherXIV Core.app
├── AetherXIV Launcher.app
├── Database/
└── build-manifest.txt
```

The server payload is embedded inside `AetherXIV Core.app`. The Launcher bundle
contains both Windows helper architectures and the Umbra payload needed by Wine.

## Output reset warning

The build recreates the selected macOS package and cleans repository-owned build
work. It also removes unexpected top-level content under `bin`. Never store
hand-written files, logs, captures, or backups under `bin`.

## Verification

The build automatically validates the final release layout. Run the full source
test suites separately when preparing a release:

```bash
./tools/Development/verify-aetherxiv.sh
```

The current build produces unsigned and unnotarized local app bundles. Signing,
notarization, archive creation, and distribution are separate release steps.

## Common failures

- **SDK not found:** install SDK `10.0.203` and ensure `dotnet` resolves to it.
- **`i686-w64-mingw32-g++` missing:** install Homebrew MinGW-w64 and re-open the
  shell if its path was added recently.
- **Manifest verification failed:** do not modify canonical direct-port files
  without updating the reviewed manifest workflow.
- **NuGet restore failed:** confirm network access or the configured offline
  package cache.
