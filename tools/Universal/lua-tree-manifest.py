#!/usr/bin/env python3
"""Create or compare the self-contained packaged Lua tree inventory."""

from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path


SCHEMA = "aetherxiv.lua-tree-manifest.v1"

ADAPTATIONS = {
    "unique/fst0Town01a/PopulaceGuildlevePublisher/gontrant.lua": (
        "Restored from the official accept_leve capture: exact Gridania regional "
        "offer IDs, rewards, menu sequence, and acceptance calls."
    ),
    "unique/fst0Town01a/PopulacePassiveGLPublisher/tierney.lua": (
        "Restored from the official accept_local_leve capture: exact Gridania "
        "local offer IDs, menu sequence, and acceptance calls."
    ),
    "base/chara/npc/object/GuildleveSearchPoint.lua": (
        "Restored from the official party_battle_leve actor binding for the client-visible "
        "guildleve search-point object."
    ),
    "base/chara/npc/object/GuildleveBonusTreasureBox.lua": (
        "Restored from the official party_battle_leve actor binding for the client-visible "
        "guildleve bonus treasure object."
    ),
}


def collect(scripts_root: Path) -> dict[str, str]:
    return {
        path.relative_to(scripts_root).as_posix(): hashlib.sha256(path.read_bytes()).hexdigest()
        for path in sorted(scripts_root.rglob("*"))
        if path.is_file()
    }


def tree_hash(files: dict[str, str]) -> str:
    digest = hashlib.sha256()
    for relative_path, content_hash in files.items():
        digest.update(relative_path.encode("utf-8"))
        digest.update(b"\0")
        digest.update(bytes.fromhex(content_hash))
    return digest.hexdigest()


def payload(files: dict[str, str]) -> dict[str, object]:
    return {
        "schema": SCHEMA,
        "source": "AetherXIV packaged Data/scripts tree",
        "fileCount": len(files),
        "treeSha256": tree_hash(files),
        "files": files,
        "adaptations": ADAPTATIONS,
    }


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--scripts-root", required=True, type=Path)
    parser.add_argument("--manifest", required=True, type=Path)
    parser.add_argument("--write", action="store_true")
    args = parser.parse_args()

    scripts_root = args.scripts_root.resolve()
    manifest_path = args.manifest.resolve()
    if not scripts_root.is_dir():
        raise SystemExit(f"Lua scripts root does not exist: {scripts_root}")

    actual = payload(collect(scripts_root))
    if args.write:
        manifest_path.parent.mkdir(parents=True, exist_ok=True)
        manifest_path.write_text(json.dumps(actual, indent=2) + "\n", encoding="utf-8")
        print(f"wrote {manifest_path}: {actual['fileCount']} files, {actual['treeSha256']}")
        return 0

    if not manifest_path.is_file():
        raise SystemExit(f"Lua tree manifest does not exist: {manifest_path}")
    expected = json.loads(manifest_path.read_text(encoding="utf-8"))
    if expected.get("schema") != SCHEMA:
        raise SystemExit(f"Unsupported Lua tree manifest schema: {expected.get('schema')!r}")

    expected_files = expected.get("files")
    if not isinstance(expected_files, dict):
        raise SystemExit("Lua tree manifest has no files object")
    actual_files = actual["files"]
    missing = sorted(set(expected_files) - set(actual_files))
    extra = sorted(set(actual_files) - set(expected_files))
    changed = sorted(
        path for path in set(expected_files) & set(actual_files)
        if expected_files[path] != actual_files[path]
    )
    if missing or extra or changed or expected.get("treeSha256") != actual["treeSha256"]:
        for label, values in (("missing", missing), ("extra", extra), ("changed", changed)):
            for value in values:
                print(f"{label}: {value}")
        raise SystemExit(
            "Lua tree differs from the immutable manifest: "
            f"expected {expected.get('treeSha256')}, actual {actual['treeSha256']}"
        )

    if expected.get("fileCount") != actual["fileCount"]:
        raise SystemExit(
            f"Lua manifest file count differs: expected {expected.get('fileCount')}, actual {actual['fileCount']}"
        )
    print(f"verified {actual['fileCount']} Lua files: {actual['treeSha256']}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
