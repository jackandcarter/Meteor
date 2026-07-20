# Optional AetherXIV 2.0 Docker server

Docker is an optional headless server deployment intended primarily for a Linux
VPS or container host. It is not the supported desktop build, server-management,
Launcher, runtime, or game-client path. Normal Windows, macOS, Linux, and
SteamOS use runs the native AetherXIV Core UI and AetherXIV Launcher directly.

The Docker deployment runs only the server stack and MariaDB. macOS and Windows
can technically run the same Linux containers through Docker Desktop, but that
adds a Linux virtual machine and is unnecessary for a normal native desktop
installation. Use it there only when intentionally testing the containerized
server deployment.

## Included services

The `server` image contains:

- Map, World, and Lobby.
- Launcher Services, the optional server-side HTTP API used by AetherXIV
  Launcher. The graphical launcher and the game client are not included.
- The packaged Lua tree, static actor data, navmesh, and managed SharpNav
  dependency.
- The canonical direct-core database baseline and all packaged migrations.
- The MariaDB command-line tools needed for database checks, migrations, and
  automatic pre-migration backups.

MariaDB runs in a separate official container with its data in a named volume.
Map, World, and Lobby currently share one server container because the restored
direct-core World-to-Map route deliberately uses loopback networking. Only the
client-facing services are published to the host.

## Recommended host

| Host | Support level and requirement |
| --- | --- |
| Linux x64 or ARM64 server/VPS | Recommended optional Docker target; Docker Engine with the Compose plugin |
| macOS or Windows development host | Optional container testing only; Docker Desktop runs the Linux images in a VM. Prefer the native Core UI for normal use |
| SteamOS | Not recommended for server containers; use the native SteamOS release or a remote Linux/VPS server |

The server image and MariaDB image support `linux/amd64` and `linux/arm64`.
Docker selects the host architecture automatically for local builds.

## First start

From the repository root:

```sh
cp .env.example .env
```

Edit `.env` and set both password values. For a client running on the same
computer as Docker, leave `AETHERXIV_PUBLIC_HOST=127.0.0.1`. For LAN or internet
hosting, set it to the IP address or DNS name that game clients can reach.

Build and start the complete stack:

```sh
docker compose up --build --detach
docker compose ps
docker compose logs --follow server
```

The first start creates the direct-core database, installs every migration,
creates the restricted application account, writes the advertised World
endpoint, and starts Map, World, Lobby, and Launcher Services in order. Later
starts verify the database before starting the services.

## Published endpoints

| Purpose | Default | Exposure |
| --- | --- | --- |
| Launcher Services HTTP API | TCP `8080` | Published |
| World | TCP `54992` | Published |
| Lobby | TCP `54994` | Published |
| Map backend route | TCP `1989` | Container-only |
| MariaDB | TCP `3306` | Compose network only |

Check Launcher Services from the Docker host:

```sh
curl http://127.0.0.1:8080/launcher/status
```

For a hosted server, allow TCP 8080, 54992, and 54994 through the host firewall
and forward the same ports through NAT. Port 8080 is not required when Launcher
Services is disabled. Do not expose MariaDB directly to the internet.

Create a matching AetherXIV Launcher server profile using the host configured in
`AETHERXIV_PUBLIC_HOST` and the published Lobby, World, and Launcher Services
ports.

## Configuration

The committed `.env.example` documents the normal operator settings:

- `AETHERXIV_DB_ROOT_PASSWORD` is used only for database installation and
  migrations.
- `AETHERXIV_DB_PASSWORD` is the password used by the server processes.
- `AETHERXIV_PUBLIC_HOST` is written to world id 1 and returned to clients after
  character selection. Enter a host name or address, not a URL.
- `AETHERXIV_*_PORT` values control both container listeners and published host
  ports.
- `AETHERXIV_LAUNCHER_ALLOW_ACCOUNT_CREATE` controls launcher account creation.
- `AETHERXIV_ENABLE_LAUNCHER_SERVICES` can disable the server-side HTTP API.

The Map route remains private on `127.0.0.1` inside the server container. The
startup script keeps every `server_zones` row aligned with that internal route
without changing the public World address.

## Day-to-day operation

```sh
# Status and health
docker compose ps

# Combined service output
docker compose logs --follow server

# MariaDB output
docker compose logs --follow mariadb

# Stop while retaining all data
docker compose down

# Start an existing installation
docker compose up --detach
```

NLog files and automatic database backups are retained in the
`aetherxiv_server-logs` and `aetherxiv_database-backups` named volumes. MariaDB
data is retained in `aetherxiv_mariadb-data`.

Do not run `docker compose down --volumes` unless the database, logs, and
backups are intentionally being deleted.

## Updating

Pull the new source or switch to the desired release tag, then run:

```sh
docker compose build --pull server
docker compose up --detach
docker compose logs --follow server
```

On startup, the new image classifies the database using the same package as the
native Core UI. It creates a missing database, rebuilds an empty or pre-2.0
database after a full backup, applies missing migrations to a valid 2.0
database, and backs up/rebuilds a damaged 2.0 schema. Compatible account and
character rows are restored on a best-effort, count-verified basis.

## Building one image for both CPU architectures

Release maintainers can publish a multi-platform manifest with Buildx:

```sh
docker buildx build \
  --platform linux/amd64,linux/arm64 \
  --file Dockerfile.server \
  --tag ghcr.io/OWNER/aetherxiv-server:2.0 \
  --push \
  .
```

Consumers can then replace `aetherxiv/server:2.0` in `compose.yaml` with the
published image and omit the `build` section if they should never build from
source.

## Current boundaries

- This is a server deployment, not a containerized game client. Wine, Umbra
  injection, client patches, and graphical desktop dependencies are excluded.
- This optional headless path uses Compose commands instead of AetherXIV Core's
  graphical process controls. The normal desktop path always uses the native
  Core UI.
- The default Compose file intentionally keeps one Map process. Horizontal Map
  scaling requires separating the database's client-advertised route from its
  internal World-to-Map route before each Map process can become an independent
  container.
- Docker validation must be run on a machine with Docker Desktop/Engine or in CI;
  a normal .NET build alone cannot exercise container networking and volume
  initialization.

The release workflow performs both multi-architecture image construction and
an amd64 Compose integration test. The integration test starts MariaDB and the
complete server stack, waits for container health, queries the installed
database, checks Launcher Services, captures logs on failure, and removes its
temporary volumes afterward.
