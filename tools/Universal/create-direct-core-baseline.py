#!/usr/bin/env python3
from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path


BASE_SQL_COUNT = 68
BASELINE_ID = "20260716_000001_ffxiv_server_v2_baseline"
INCLUDED_AFTER_BASE = (
    "Data/sql/migrations/20260627_battlenpc_spawn_audit_pins.sql",
    "Data/sql/migrations/20260707_seed_level1_player_base_stats.sql",
    "db/direct-core/migrations/20260716_000001_launcher_ui_contract.sql",
    "db/direct-core/migrations/20260716_000004_database_compatibility.sql",
)
FORBIDDEN_RELEASE_MARKERS = (
    "server_battlenpc_appearance_audit",
    "server_battlenpc_restoration_evidence",
    "client_decode_import_batches",
    "client_decoded_actor_class_stage",
    "client_decoded_actor_graphic_stage",
    "client_decoded_display_name_stage",
    "TEST_ONLY / Hypothesis placement",
)


def digest(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--repo-root", required=True, type=Path)
    parser.add_argument("--output-dir", required=True, type=Path)
    args = parser.parse_args()

    root = args.repo_root.resolve()
    output = args.output_dir.resolve()
    base_files = sorted((root / "Data/sql").glob("*.sql"))
    if len(base_files) != BASE_SQL_COUNT:
        raise SystemExit(f"Expected {BASE_SQL_COUNT} direct-core base SQL files; found {len(base_files)}")

    source_files = [*base_files, *(root / relative for relative in INCLUDED_AFTER_BASE)]
    missing = [path for path in source_files if not path.is_file()]
    if missing:
        raise SystemExit("Missing baseline input: " + ", ".join(str(path) for path in missing))

    chunks: list[bytes] = [
        b"-- AetherXIV direct-core database baseline.\n",
        f"-- baseline_id: {BASELINE_ID}\n".encode(),
        b"-- Generated deterministically from source-owned SQL; do not edit this output.\n\n",
    ]
    sources: list[dict[str, str]] = []
    for path in source_files:
        relative = path.relative_to(root).as_posix()
        # Git may check text files out with CRLF on Windows. The canonical
        # baseline must be byte-identical on every build host.
        content = path.read_bytes().replace(b"\r\n", b"\n").replace(b"\r", b"\n")
        sources.append({"path": relative, "sha256": digest(content)})
        chunks.extend((f"\n-- BEGIN {relative}\n".encode(), content, f"\n-- END {relative}\n".encode()))

    baseline = b"".join(chunks)
    decoded = baseline.decode("utf-8", errors="replace")
    present = [marker for marker in FORBIDDEN_RELEASE_MARKERS if marker in decoded]
    if present:
        raise SystemExit("Development/provisional SQL leaked into baseline: " + ", ".join(present))

    output.mkdir(parents=True, exist_ok=True)
    baseline_path = output / "ffxiv_server.sql"
    baseline_path.write_bytes(baseline)
    baseline_hash = digest(baseline)
    (output / "ffxiv_server.sql.sha256").write_text(
        f"{baseline_hash}  ffxiv_server.sql\n", encoding="utf-8"
    )
    manifest = {
        "schema": "aetherxiv.database.baseline.v1",
        "baselineId": BASELINE_ID,
        "sha256": baseline_hash,
        "sources": sources,
    }
    (output / "baseline-manifest.json").write_text(
        json.dumps(manifest, indent=2) + "\n", encoding="utf-8"
    )
    print(f"Created {baseline_path} ({baseline_hash}) from {len(source_files)} source files.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
