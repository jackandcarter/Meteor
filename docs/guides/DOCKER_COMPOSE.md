# Docker Compose Local Stack

This runs the full AetherXIV 1.3 stack (MariaDB, Lobby, World, Map, and the
PHP web/launcher service) as containers, without installing MariaDB/PHP/Mono
on the host. It is an alternative to the native per-platform guides, not a
replacement for them; Echo Gate and the client still run on the host either
way.

## 1. Prerequisites

- Docker Desktop (or another engine) with Compose v2, so `docker compose`
  works from the repo root.
- A copy of `.env` at the repo root. One is already checked out with local
  defaults; adjust it if a knob collides with something else on your
  machine (see the port table below).

## 2. Quick Start

From the repository root:

```sh
docker compose up -d --build
docker compose ps
```

First boot builds the `aetherxiv-13:local` server image and the
`aetherxiv-13-web:local` web image, starts MariaDB, seeds the database from
`Data/sql/*.sql`, applies `Data/sql/migrations/*.sql`, then starts web,
lobby, world, and map. `docker compose ps` should show all of them healthy
within a couple of minutes on first boot (the MariaDB seed import is the
slow part).

```sh
docker compose logs -f lobby
docker compose down          # stop, keep the DB volume
```

## 3. Services

| Service   | Container port | Host port (default) | Purpose |
|-----------|-----------------|----------------------|---------|
| mariadb   | 3306            | `AETHER_DB_BIND` (13306 in the checked-out `.env`; compose default 3306) | Database |
| db-init   | n/a             | n/a                  | One-shot migration runner, then exits |
| web       | 8080            | `AETHER_WEB_BIND` (8080) | PHP launcher/news/account service |
| lobby     | 54994           | `AETHER_LOBBY_BIND` (54994) | Lobby server (also carries world/map, see below) |
| world     | 54992           | `AETHER_WORLD_BIND` (54992) | World server |
| map       | 1989 (loopback only) | none | Map server; world relays all zone traffic, clients never dial it |

`world` and `map` run with `network_mode: "service:lobby"`, sharing lobby's
network namespace instead of getting their own. This keeps the DB-seeded
`127.0.0.1` addresses in `servers.address` / `server_zones.serverIp` working
the same way they do in the native (non-container) setup, where all three
servers run as processes on one host. Only `lobby` publishes ports; adding a
`ports:` entry to `world` or `map` is a compose error in this network mode
and is deliberately left out.

The checked-out `.env` sets `AETHER_DB_BIND=13306` because this machine's
host `mysqld` already owns 3306. If you don't have a conflicting local
MariaDB, you can lower it back to 3306.

## 4. First-Boot Database Seeding

`Data/sql/*.sql` (68 files) is mounted read-only at
`/docker-entrypoint-initdb.d` on the `mariadb` container. MariaDB's own
entrypoint runs every `*.sql` file there, alphabetically, but **only when
the `aetherxiv13-db-data` volume is empty** (i.e. the very first time the
container starts against a fresh volume). Once the volume has data, this
step is skipped on every later `docker compose up`, even after a rebuild.

`Data/sql/migrations/` is not part of that seed step (the MariaDB
entrypoint only looks at the top level of the mounted directory). Instead,
the `db-init` service runs `tools/apply-db-migrations.sh` against the
running `mariadb` container every time the stack comes up. That script is
checksum-guarded through the `aether_schema_migrations` ledger table: each
migration file's SHA-256 is recorded once it is applied, so a migration
that already ran is skipped (and a migration file whose content changed
after being applied is flagged with a warning rather than silently
re-applied). `lobby`, `world`, `map`, and `web` all wait on `db-init` to
exit 0 before they start.

## 5. Re-Seeding From Scratch

To wipe the database and re-run the full `Data/sql` seed:

```sh
docker compose down -v
docker compose up -d --build
```

`-v` drops the `aetherxiv13-db-data` named volume. Without `-v`, `down` and
`up` again reuse the existing volume and only `db-init`'s migration step
runs.

## 6. Static Actor Data

The server image builds without `Data/staticactors.bin` (it is
gitignored and machine-specific). `Data/` is bind-mounted read-only into
the server containers at `/opt/aetherxiv/Data`, so a file placed there on
the host appears in the containers without a rebuild. To prepare it:

```sh
./tools/prepare-client-data.sh --client-dir "/path/to/FINAL FANTASY XIV"
docker compose restart --no-deps map world
```

Use the `--client-dir` flag rather than the `CLIENT_DIR` env var: the
script sources `.env.defaults`, whose empty `CLIENT_DIR=` line overrides
an environment-provided value.

Until `staticactors.bin` exists, `map` refuses to start (SMOKE_FAIL,
exit 40) and stays in a restart loop, which also leaves the world
server's zone connection down - hence restarting both above.

## 7. Connecting Echo Gate

Point Echo Gate (running on the host, same as the native setup) at the
compose stack using the same default ports the native `tools/run-*.sh`
scripts use:

- Server tab launcher service: `http://127.0.0.1:8080/launcher` (or
  whatever `AETHER_WEB_BIND` maps to).
- Lobby connection: `127.0.0.1:54994` (or whatever `AETHER_LOBBY_BIND`
  maps to).

No Echo Gate-side configuration differs between the native and
containerized stack; only which process is listening on those ports
changes.

## 8. Troubleshooting

```sh
docker compose ps                 # service health at a glance
docker compose logs -f mariadb    # first-boot seed progress
docker compose logs -f db-init    # migration ledger output
docker compose logs -f lobby world map web
```

Open a database shell inside the running `mariadb` container:

```sh
docker compose exec mariadb mariadb -uaetherxiv -paether_dev ffxiv_server
```

(substitute your `AETHER_DB_PASS` if you changed it from the default).

If a server container is unhealthy, check its ready file directly:

```sh
docker compose exec lobby test -f /tmp/lobby.ready && echo ready
```

If `world` or `map` won't start, confirm `lobby` is at least started
(`docker compose ps lobby`) since both depend on lobby's network
namespace, not just on MariaDB.
