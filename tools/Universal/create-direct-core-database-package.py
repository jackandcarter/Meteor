#!/usr/bin/env python3
from __future__ import annotations

import argparse
import shutil
import subprocess
from pathlib import Path


LEGACY_MIGRATIONS = (
    "Data/sql/migrations/20260627_battlenpc_spawn_audit_pins.sql",
    "Data/sql/migrations/20260707_seed_level1_player_base_stats.sql",
)


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--repo-root", required=True, type=Path)
    parser.add_argument("--output-dir", required=True, type=Path)
    args = parser.parse_args()

    root = args.repo_root.resolve()
    output = args.output_dir.resolve()
    if output.exists():
        shutil.rmtree(output)
    output.mkdir(parents=True, exist_ok=True)
    subprocess.run(
        [
            "python3",
            str(root / "tools/Universal/create-direct-core-baseline.py"),
            "--repo-root",
            str(root),
            "--output-dir",
            str(output),
        ],
        check=True,
    )

    migrations = output / "migrations"
    migrations.mkdir(exist_ok=True)
    direct_core_migrations = sorted(
        (root / "db/direct-core/migrations").glob("*.sql"),
        key=lambda path: path.name,
    )
    migration_sources = [root / relative for relative in LEGACY_MIGRATIONS]
    migration_sources.extend(direct_core_migrations)
    if not direct_core_migrations:
        raise SystemExit("No direct-core migrations were found to package.")

    packaged_names: set[str] = set()
    for source in migration_sources:
        if not source.is_file():
            raise SystemExit(f"Missing direct-core migration: {source}")
        if source.name in packaged_names:
            raise SystemExit(f"Duplicate packaged migration name: {source.name}")
        packaged_names.add(source.name)
        shutil.copy2(source, migrations / source.name)

    for name in ("setup.sh", "setup.ps1"):
        source = root / "db/direct-core" / name
        if not source.is_file():
            raise SystemExit(f"Missing database setup entry: {source}")
        destination = output / name
        shutil.copy2(source, destination)
        if name.endswith(".sh"):
            destination.chmod(0o755)

    print(f"Packaged direct-core database installer at {output}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
