# AetherXIV 2.0

AetherXIV 2.0 is a modern server-core and launcher foundation for FFXIV 1.23b Client.
The workspace is designed for long-term cross-platform development, clean service boundaries, testable packet and scripting behavior,
and a database foundation that can grow into a fully seeded asset.

## Supported Platforms

The 2.0 target platforms are:

- macOS
- Linux
- Steam Deck / SteamOS
- Windows

Platform-specific developer and operator tooling will live under:

- `tools/MacOS`
- `tools/Linux`
- `tools/SteamDeck`
- `tools/Windows`
- `tools/Universal`

Those tool folders are intentionally early placeholders. The core server and
backend foundations come first; platform tooling will be filled in as the 2.0
runtime becomes stable enough to support real setup, diagnostics, packaging,
and operation workflows.

## Languages And Runtime

- C# on .NET 10 LTS for server cores, shared services, protocol code, data
  access, hosting, and EchoGate.Next foundations.
- Lua for gameplay and event scripting.
- SQL for MariaDB/MySQL schema and migration work.

The pinned SDK is declared in `global.json`:

```sh
/usr/local/share/dotnet/dotnet --version
```

Expected SDK:

```text
10.0.203
```

## Primary Dependencies

- .NET 10 SDK
- MariaDB or MySQL
- MoonSharp for Lua hosting
- xUnit for automated tests
- `MySqlConnector` for async database access
- `Microsoft.Extensions.*` libraries for hosting, logging, configuration, and
  dependency injection

## Workspace Layout

- `src/AetherXIV.Protocol` - packet primitives, codecs, opcodes, binary helpers
- `src/AetherXIV.Core` - shared identifiers and core contracts
- `src/AetherXIV.Data` - database models, migrations, repository contracts
- `src/AetherXIV.Scripting` - Lua host, script contracts, coroutine scheduler
- `src/AetherXIV.Server.Hosting` - server loop and hosting primitives
- `src/AetherXIV.Lobby` - lobby service foundation
- `src/AetherXIV.World` - world service foundation
- `src/AetherXIV.Map` - map service, event dispatch, actor/script boundaries
- `src/AetherXIV.Compatibility` - compatibility fixtures and provenance rules
- `src/EchoGate.Next.Core` - launcher/profile/client validation foundation
- `db/migrations` - database schema migrations
- `tests` - focused unit and compatibility tests

## Build And Test

From the repository root:

```sh
/usr/local/share/dotnet/dotnet test AetherXIV.sln
```

The solution should resolve SDK `10.0.203` through `global.json`.

Some Lua compatibility tests can also run against an external script fixture
tree. Set `AETHERXIV_SCRIPT_FIXTURE_ROOT` to the absolute path of a
`Data/scripts` directory before running tests when that fixture tree is
available.

## Docker

A local stack (MariaDB plus the three service processes) is available via
Docker Compose. Prerequisites: Docker with Compose v2 (`docker compose`, not
the standalone `docker-compose`).

```sh
cp .env.example .env   # optional, defaults work as-is
docker compose up -d --build
```

| Service | Host port | Notes |
|---------|-----------|-------|
| mariadb | 3306 | app database `aetherxiv2`, user `aetherxiv` |
| lobby   | 54994 | |
| world   | 54992 | |
| map     | 1989 | |

All three services run from one built image (`aetherxiv-server:local`);
`AETHERXIV_SERVICE` (or a `lobby`/`world`/`map` CLI argument when running the
image standalone) selects which one starts.

The schema seeds from `db/migrations` on the first boot of an empty database
volume only. To force a re-seed:

```sh
docker compose down -v
docker compose up -d --build
```

Tail a service's logs:

```sh
docker compose logs -f lobby
```

Connect to the database:

```sh
docker compose exec mariadb mariadb -uaetherxiv -paether_dev aetherxiv2
```

Stop the stack (keeps the database volume):

```sh
docker compose down
```

## Development Direction

The first development goal is a correct, testable foundation:

- byte-exact protocol codecs
- async service boundaries
- typed database access
- Lua scripting compatibility
- explicit diagnostics
- platform-aware tooling
- provenance-backed data migration

2.0 is not ready to replace any live stack yet. It is the foundation branch for
building that future cleanly.
