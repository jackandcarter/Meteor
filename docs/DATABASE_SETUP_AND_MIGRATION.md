# Database Setup, Updates, and Recovery

AetherXIV Core uses MariaDB. The release contains the canonical `ffxiv_server`
baseline, ordered migrations, checksums, and setup scripts. For normal local
installations, use the graphical AetherXIV Core workflow.

## Defaults

| Setting | Default |
|---|---|
| Host | `127.0.0.1` |
| Port | `3306` |
| Database | `ffxiv_server` |
| Application user | `aetherxiv` |
| Development password | `aether_dev` |

Change the application password before exposing a server to other computers.
Database port `3306` should remain local or restricted to trusted hosts.

## First-time setup in AetherXIV Core

1. Install and start [MariaDB Community Server](https://mariadb.org/download/).
2. Open AetherXIV Core and select **Config**.
3. Confirm the database host, port, name, user, and password.
4. Leave **Auto setup/repair** enabled for a normal local installation.
5. Select **Verify Dependencies**.
6. Select **Start Stack**.
7. When requested, enter a MariaDB administrator account that can create the
   database and application user.

The administrator password is used for that operation and is not saved. The
application database password is saved in the Core settings, so do not upload
that file in a bug report without redacting it.

This flow handles both a completely absent database and a manually created
empty schema. In either case, it creates the restricted application account
before the final verification pass.

## What automatic setup does

The database package:

- creates the configured database when it does not exist;
- creates or updates the local application account;
- imports the canonical baseline;
- validates baseline and migration checksums;
- applies unapplied migrations in order;
- verifies the AetherXIV 2 version record, migration ledger, required tables,
  columns, and seed data;
- backs up any existing database before replacing or modifying it.

Setup handles each state explicitly:

| Existing state | Action |
|---|---|
| Database absent | Create the database and restricted application account, then install and verify AetherXIV 2 |
| Empty database or pre-2.0 database | Keep a full backup, recreate the canonical database, and try to restore compatible account/character data |
| Valid AetherXIV 2 database | Check the migration ledger, apply missing migrations, and verify required tables, columns, and seeds |
| Damaged or incomplete AetherXIV 2 database | Keep a full backup, rebuild the canonical schema, and try to restore compatible account/character data |

## Command-line administration

The `Database` folder in every release contains both `setup.sh` and `setup.ps1`.
These are intended for administrators, automation, and recovery.

Check an existing database without modifying it:

```bash
./Database/setup.sh --check
```

```powershell
.\Database\setup.ps1 -Check
```

Run normal setup or apply pending migrations:

```bash
./Database/setup.sh
```

```powershell
.\Database\setup.ps1
```

The shell script accepts configuration through `AETHERXIV_DB_*` environment
variables. Run either script with its help option before using destructive or
clean-migration modes.

## Backups and clean migration

Existing databases are dumped before changes. The shell package defaults to
`~/.aetherxiv/backups/database`; the Core application can supply its own backup
location. Backups receive a companion SHA-256 file.

Canonical rebuild always creates a full verified backup and recreates the
canonical schema. If both legacy `users` and `characters` tables exist, it also
creates a player-data export and attempts to restore accounts, characters,
and tables whose names begin with `characters_`. Account and character counts
must match.

If player data is incompatible, the installer keeps the clean canonical
database and retains both exports for manual recovery. It restores the
untouched full backup automatically only when the canonical database itself
cannot be installed or rebuilt safely.

Always retain an independent copy of the backup before a major upgrade.

## Do not edit applied migrations

Applied migrations are recorded with their SHA-256 checksum. Changing an
already-applied migration causes verification to fail. Correct a released
migration by adding a new migration with a later name.

## Common failures

- **Cannot connect:** confirm the MariaDB service is running and the host/port
  are correct.
- **Access denied:** use a MariaDB administrator account during setup, then
  verify the configured application password.
- **Checksum mismatch:** restore the original release database package; do not
  bypass the check.
- **Pre-2.0 or damaged database:** use Core's backed-up canonical repair. Review the
  retained backup if automatic player-data restoration was not possible.
- **Port already in use:** determine whether another MariaDB instance owns port
  `3306`, then change the configured port consistently if required.
