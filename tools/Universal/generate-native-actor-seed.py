#!/usr/bin/env python3
"""Build the packaged native static-actor seed from reviewed import artifacts."""

from __future__ import annotations

import argparse
import hashlib
import json
import re
from pathlib import Path


UNQUOTED_KEY = re.compile(r'([,{]\s*)([A-Za-z_][A-Za-z0-9_]*)\s*:')


def read_json(path: Path):
    return json.loads(path.read_text())


def write_json(path: Path, value) -> str:
    content = json.dumps(value, ensure_ascii=True, separators=(",", ":"), sort_keys=True) + "\n"
    path.write_text(content)
    return hashlib.sha256(content.encode("utf-8")).hexdigest()


def normalize_event_conditions(actor_class: dict) -> None:
    value = actor_class["EventConditions"]
    try:
        json.loads(value)
        return
    except json.JSONDecodeError:
        repaired = UNQUOTED_KEY.sub(r'\1"\2":', value)
        json.loads(repaired)
        actor_class["EventConditions"] = repaired
        provenance = actor_class.get("Provenance", {})
        notes = provenance.get("Notes", "")
        provenance["Notes"] = f"{notes} Invalid legacy object-literal keys normalized to JSON for the native seed.".strip()


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--review-root", required=True, type=Path)
    parser.add_argument("--output", required=True, type=Path)
    parser.add_argument("--version", required=True)
    args = parser.parse_args()

    classes = read_json(args.review_root / "actor-classes.json")
    appearances = read_json(args.review_root / "actor-appearances.json")
    spawns = read_json(args.review_root / "static-actor-spawns.json")
    zones = read_json(args.review_root / "zones.json")
    zone_ids = {row["Id"]["Value"] for row in zones}
    class_rows_by_id = {row["ActorClassId"]: row for row in classes}
    invalid_class_ids = {
        actor_class_id
        for actor_class_id, row in class_rows_by_id.items()
        if not row["ClassPath"].strip()
    }
    excluded_spawns = [row for row in spawns if row["ZoneId"]["Value"] not in zone_ids]
    excluded_invalid_class_spawns = [
        row
        for row in spawns
        if row["ZoneId"]["Value"] in zone_ids and row["ActorClassId"] in invalid_class_ids
    ]
    spawns = [
        row
        for row in spawns
        if row["ZoneId"]["Value"] in zone_ids and row["ActorClassId"] not in invalid_class_ids
    ]
    referenced_ids = {row["ActorClassId"] for row in spawns}
    classes = [row for row in classes if row["ActorClassId"] in referenced_ids]
    appearances = [row for row in appearances if row["ActorClassId"] in referenced_ids]
    for actor_class in classes:
        normalize_event_conditions(actor_class)

    class_ids = {row["ActorClassId"] for row in classes}
    missing_classes = sorted(referenced_ids - class_ids)
    if missing_classes:
        raise ValueError(f"Static actor seed has missing actor classes: {missing_classes[:16]}")

    args.output.mkdir(parents=True, exist_ok=True)
    hashes = {
        "zones.json": write_json(args.output / "zones.json", zones),
        "actor-classes.json": write_json(args.output / "actor-classes.json", classes),
        "actor-appearances.json": write_json(args.output / "actor-appearances.json", appearances),
        "static-actor-spawns.json": write_json(args.output / "static-actor-spawns.json", spawns),
    }
    manifest = {
        "schema": "aetherxiv.native-actor-seed.v1",
        "seedId": "static-actor-catalog",
        "version": args.version,
        "zoneCount": len(zones),
        "actorClassCount": len(classes),
        "actorAppearanceCount": len(appearances),
        "staticActorSpawnCount": len(spawns),
        "excludedOrphanSpawnCount": len(excluded_spawns),
        "excludedInvalidActorClassSpawnCount": len(excluded_invalid_class_spawns),
        "files": hashes,
        "notes": "Reviewed static actor catalog consumed by native AetherXIV 2.0 runtime repositories.",
    }
    write_json(args.output / "manifest.json", manifest)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
