#!/usr/bin/env python3
"""Apply reviewed actor overrides and refresh the native seed manifest."""

from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path


FILES = (
    ("actor-classes.json", "actorClasses", "ActorClassId"),
    ("actor-appearances.json", "actorAppearances", "ActorClassId"),
    ("static-actor-spawns.json", "staticActorSpawns", "SpawnId"),
)


def write_json(path: Path, value: object) -> str:
    content = json.dumps(value, ensure_ascii=True, separators=(",", ":"), sort_keys=True) + "\n"
    path.write_text(content, encoding="utf-8")
    return hashlib.sha256(content.encode("utf-8")).hexdigest()


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--catalog", required=True, type=Path)
    parser.add_argument("--overrides", required=True, type=Path)
    parser.add_argument("--version", required=True)
    args = parser.parse_args()

    catalog = args.catalog.resolve()
    overrides = json.loads(args.overrides.read_text(encoding="utf-8"))
    if overrides.get("schema") != "aetherxiv.native-actor-approved-overrides.v1":
        raise SystemExit("Unsupported approved actor override schema")

    manifest_path = catalog / "manifest.json"
    manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    hashes: dict[str, str] = {}
    counts: dict[str, int] = {}
    for file_name, section, key in FILES:
        path = catalog / file_name
        rows = json.loads(path.read_text(encoding="utf-8"))
        by_id = {row[key]: row for row in rows}
        for row in overrides.get(section, []):
            by_id[row[key]] = row
        merged = [by_id[row_id] for row_id in sorted(by_id)]
        hashes[file_name] = write_json(path, merged)
        counts[section] = len(merged)

    zones_path = catalog / "zones.json"
    hashes["zones.json"] = hashlib.sha256(zones_path.read_bytes()).hexdigest()
    manifest["version"] = args.version
    manifest["actorClassCount"] = counts["actorClasses"]
    manifest["actorAppearanceCount"] = counts["actorAppearances"]
    manifest["staticActorSpawnCount"] = counts["staticActorSpawns"]
    manifest["files"] = hashes
    write_json(manifest_path, manifest)
    print(
        f"updated {catalog}: {counts['actorClasses']} classes, "
        f"{counts['actorAppearances']} appearances, {counts['staticActorSpawns']} spawns"
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
