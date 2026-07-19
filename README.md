# AetherXIV 2.0

AetherXIV 2.0 is a modern, cross-platform server and launcher stack for a
user-owned Final Fantasy XIV 1.23b client. It combines the Lobby, World, Map,
Launcher Services, AetherXIV Core management app, AetherXIV Launcher, database
tooling, and the Umbra plugin framework in one workspace.

## Supported release targets

- macOS 14 or later on Apple silicon
- Windows 11 x64
- Ubuntu 22.04/24.04 x64
- SteamOS in Desktop Mode

## Getting started

- [Documentation index](docs/README.md)
- [AetherXIV Core guide](docs/AETHERXIV_CORE_GUIDE.md)
- [Launcher guide](docs/LAUNCHER_GUIDE.md)
- [Database setup and migration](docs/DATABASE_SETUP_AND_MIGRATION.md)
- [Build and runtime dependencies](docs/BUILD_AND_RUNTIME_DEPENDENCIES.md)
- [AetherXIV 2.0 release notes](docs/AETHERXIV_2.0_RELEASE_NOTES.md)

## Build and test

The repository pins .NET SDK `10.0.203` in `global.json`. Run the complete
managed verification suite from the repository root:

```sh
./tools/Development/verify-aetherxiv.sh
```

Platform release builds use the dedicated scripts under `tools/MacOS`,
`tools/Linux`, `tools/SteamOS`, and `tools/Windows`. Generated release output is
written beneath the ignored `bin/build/Release` directory.

## Client ownership

AetherXIV does not distribute the Final Fantasy XIV client, patches, or Square
Enix assets. Each user must provide a legally obtained Final Fantasy XIV 1.23b
client and any required patch library.
