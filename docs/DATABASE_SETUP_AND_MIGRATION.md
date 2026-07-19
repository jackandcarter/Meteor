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

## What automatic setup does

The database package:

- creates the configured database when it does not exist;
- creates or updates the local application account;
- imports the canonical baseline;
- validates baseline and migration checksums;
- applies unapplied migrations in order;
- verifies required tables, seed data, and compatibility identifiers;
- backs up an existing recognized database before modifying it.

If an existing database cannot be recognized as an AetherXIV direct-core
database, automatic migration is refused rather than guessing.

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

Clean migration preserves recognized player and launcher-content tables,
recreates the canonical schema, restores the preserved data, and verifies that
account and character counts match. If restoration or verification fails, the
installer attempts to restore the untouched full backup.

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
- **Unrecognized database:** back it up and review its schema before attempting
  manual conversion.
- **Port already in use:** determine whether another MariaDB instance owns port
  `3306`, then change the configured port consistently if required.
