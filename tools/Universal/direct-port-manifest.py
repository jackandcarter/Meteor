#!/usr/bin/env python3
"""Capture and verify immutable source provenance for the direct server port."""

from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path


COMPONENTS = (
    ("Common", "Common Class Lib", "src/AetherXIV.Core.Common"),
    ("Lobby", "Lobby Server", "src/AetherXIV.Core.Lobby"),
    ("World", "World Server", "src/AetherXIV.Core.World"),
    ("Map", "Map Server", "src/AetherXIV.Core.Map"),
)

EXPECTED_SOURCE_COUNTS = {
    "Common": 14,
    "Lobby": 27,
    "World": 77,
    "Map": 300,
}

RUNTIME_ASSETS = (
    ("Lobby/NLog.config", "Lobby Server/NLog.config", "src/AetherXIV.Core.Lobby/NLog.config"),
    ("World/NLog.config", "World Server/NLog.config", "src/AetherXIV.Core.World/NLog.config"),
    ("Map/NLog.config", "Map Server/NLog.config", "src/AetherXIV.Core.Map/NLog.config"),
    ("Map/staticactors.bin", "Data/staticactors.bin", "Data/seeds/static-actors/staticactors.bin"),
    ("Map/navmesh/wil0Field01.snb", "Map Server/navmesh/wil0Field01.snb", "Data/navmesh/wil0Field01.snb"),
    ("Map/navmesh/SHARPNAV_LICENSE", "Map Server/navmesh/SHARPNAV_LICENSE", "Data/navmesh/SHARPNAV_LICENSE"),
)

ADAPTATIONS = {
    "Common/DevDiagnostics.cs": (
        "Environment variable names were updated to the AetherXIV product prefix without "
        "changing diagnostic enablement or output behavior."
    ),
    "Common/StartupReadySignal.cs": (
        "The ready-file environment variable was updated to the AetherXIV product prefix; "
        "the file format and signaling behavior are unchanged."
    ),
    "Map/Packets/Send/Actor/Inventory/SetInitialEquipmentPacket.cs": (
        "The source existed outside the old project compile list. Its stale Lobby import was "
        "replaced with Common, and the intended target actor is now assigned through the "
        "existing SubPacket.SetTargetId API. Packet fields and payload construction are unchanged."
    ),
    "Lobby/Program.cs": (
        "The modern host fixes the process working directory to AppContext.BaseDirectory so the "
        "legacy relative config paths resolve identically in centralized and published builds. "
        "The legacy DEBUG console listener is registered through Trace.Listeners, the supported "
        ".NET 10 equivalent of the removed Debug.Listeners API."
    ),
    "World/Program.cs": (
        "The modern host fixes the process working directory to AppContext.BaseDirectory so the "
        "legacy relative config paths resolve identically in centralized and published builds. "
        "The legacy DEBUG console listener is registered through Trace.Listeners, the supported "
        ".NET 10 equivalent of the removed Debug.Listeners API."
    ),
    "Map/Program.cs": (
        "The modern host fixes the process working directory to AppContext.BaseDirectory so the "
        "legacy relative config, static-actor, and Lua paths resolve identically in centralized and published builds. "
        "The legacy DEBUG console listener is registered through Trace.Listeners, the supported "
        ".NET 10 equivalent of the removed Debug.Listeners API."
    ),
    "Map/Utils/NavmeshUtils.cs": (
        "The legacy navmesh file is loaded from the packaged application base directory instead of traversing "
        "the process working directory. The SharpNav serializer, file contents, and pathfinding behavior are unchanged."
    ),
    "Lobby/ConfigConstants.cs": (
        "The existing legacy launch-argument adapter accepts the database port supplied by the AetherXIV Core app."
    ),
    "World/ConfigConstants.cs": (
        "The existing legacy launch-argument adapter accepts the database port supplied by the AetherXIV Core app."
    ),
    "Map/ConfigConstants.cs": (
        "The existing legacy launch-argument adapter accepts the database port supplied by the AetherXIV Core app."
    ),
    "Map/Actors/Area/Area.cs": (
        "The actor-number allocator is shared by spawned actors and directors, matching the official unified "
        "zone actor sequence and preventing recycled or colliding IDs after dynamic despawns. The existing "
        "private-area range remains unchanged."
    ),
    "Map/Actors/Director/Director.cs": (
        "Official party_battle_leve traffic identifies directors in the zone NPC actor family, includes the "
        "director in its content group, and uses guildleve group type 30001. The modern port now emits those "
        "same wire identities and bypasses only the known placeholder timed Lua main for trace-restored content."
    ),
    "Map/Actors/Director/GuildleveDirector.cs": (
        "Guildleve 12487 initial content is restored directly from party_battle_leve: the battle-orb position, "
        "bonus object, eleven search points, objective properties, markers, and eligible party membership. "
        "Enemy waves and combat behavior are deliberately unchanged."
    ),
    "Map/Actors/Group/ContentGroup.cs": (
        "The content director is included in its own member list as observed in official guildleve group "
        "packets; duplicate initial members are suppressed."
    ),
    "Map/WorldManager.cs": (
        "Database-backed battle NPC spawning uses the existing area allocator instead of bypassing it with "
        "the current actor count, preventing public event-owner IDs from being reused during private content entry. "
        "When a persisted opening SimpleContent area is absent after restart, the runtime now reconstructs it "
        "through the existing legacy CreateContentArea/content-script/director path instead of clearing the private-area "
        "boundary and projecting battle-side quest flags onto the public grounded-NPC scene. Phase 10 continues to the "
        "legacy post-battle area."
    ),
    "Map/DataObjects/Session.cs": (
        "Session ending is idempotent so a later World close confirmation cannot extend the Map cleanup grace window."
    ),
    "Map/PacketProcessor.cs": (
        "A locally completed logout or quit is not saved and removed a second time when World observes the socket close. "
        "The existing World session-end confirmation is still emitted."
    ),
    "Map/Actors/Chara/Npc/BattleNpc.cs": (
        "The stray unconditional claimed-hate assignment after the existing passive/engaged/party calculation "
        "is removed; no packet structure or combat formula is changed."
    ),
    "Map/Actors/Chara/Player/Player.cs": (
        "Player.SetEventStatus now queues the 0x0136 packet it constructs. Official server-to-client traces "
        "confirm 0x0136 as the actor event-status contract, and the legacy city scripts require this method "
        "for player-specific event-condition overrides. Structured diagnostics record each queued override. "
        "During persisted opening-content reconstruction, the active content director satisfies the unchanged "
        "player.lua OpeningDirector existence check so login cannot start a contradictory public director over "
        "the restored battle; structured diagnostics record that compatibility resolution. "
        "Logout and QuitGame now mark the Map session ending immediately after queuing the unchanged client transition "
        "packet, closing the observed post-cleanup client-update race while preserving their distinct confirmation flow. "
        "The legacy bonus-point stub is completed with the exact class-scoped allocation limits and values observed in "
        "the official add_str exchange; invalid client submissions are rejected before persistence or stat recalculation. "
        "The later experimental equipment-param summation is removed because it treated conditional item metadata as "
        "unconditional Modifier values and is contradicted by the official equip-change captures. The trace-confirmed "
        "weapon damage type, delay, and hit-count mapping remains unchanged while equipment scaling is reconstructed."
    ),
    "Map/Actors/Chara/ReferencedItemPackage.cs": (
        "Equipment link changes emit structured diagnostics with player, slot, old/new item IDs, class, level, and the "
        "explicit fact that stat recalculation remains deferred. Inventory mutation, persistence, and packet ordering "
        "are unchanged; trade-package changes do not emit equipment diagnostics."
    ),
    "Map/Database.cs": (
        "Attribute-point allocation is persisted atomically in the existing characters_class_attributes table before "
        "the in-memory player state is changed, completing the official bonus-point exchange without adding a parallel "
        "progression store or changing legacy stat formulas."
    ),
}


def sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def source_files(root: Path) -> list[Path]:
    return sorted(path for path in root.rglob("*.cs") if "bin" not in path.parts and "obj" not in path.parts)


def capture(reference_root: Path, repo_root: Path, manifest: Path) -> int:
    entries: list[dict[str, str]] = []
    for component, reference_dir, port_dir in COMPONENTS:
        origin_root = reference_root / reference_dir
        destination_root = repo_root / port_dir
        origins = source_files(origin_root)
        expected_count = EXPECTED_SOURCE_COUNTS[component]
        if len(origins) != expected_count:
            raise SystemExit(
                f"Unexpected {component} source count: {len(origins)}; expected {expected_count}"
            )
        for origin in origins:
            relative = origin.relative_to(origin_root)
            destination = destination_root / relative
            if not destination.is_file():
                raise SystemExit(f"Missing ported source: {destination}")
            origin_key = f"{component}/{relative.as_posix()}"
            origin_hash = sha256(origin)
            port_hash = sha256(destination)
            exact = origin_hash == port_hash
            if not exact and origin_key not in ADAPTATIONS:
                raise SystemExit(f"Unrecorded source adaptation: {origin_key}")
            entries.append(
                {
                    "originPath": origin_key,
                    "originSha256": origin_hash,
                    "portPath": destination.relative_to(repo_root).as_posix(),
                    "portSha256": port_hash,
                    "disposition": "exact-copy" if exact else "compile-adaptation",
                    "reason": "" if exact else ADAPTATIONS[origin_key],
                }
            )

    runtime_assets: list[dict[str, str]] = []
    for asset_key, reference_path, port_path in RUNTIME_ASSETS:
        origin = reference_root / reference_path
        destination = repo_root / port_path
        if not origin.is_file():
            raise SystemExit(f"Missing reference runtime asset: {origin}")
        if not destination.is_file():
            raise SystemExit(f"Missing ported runtime asset: {destination}")
        origin_hash = sha256(origin)
        port_hash = sha256(destination)
        if origin_hash != port_hash:
            raise SystemExit(f"Runtime asset differs from reference: {asset_key}")
        runtime_assets.append(
            {
                "originPath": asset_key,
                "originSha256": origin_hash,
                "portPath": port_path,
                "portSha256": port_hash,
            }
        )

    sql_reference_root = reference_root / "Data/sql"
    sql_port_root = repo_root / "Data/sql"
    sql_origins = sorted(sql_reference_root.rglob("*.sql"))
    if len(sql_origins) != 80:
        raise SystemExit(f"Unexpected SQL source count: {len(sql_origins)}; expected 80")
    for origin in sql_origins:
        relative = origin.relative_to(sql_reference_root)
        destination = sql_port_root / relative
        if not destination.is_file():
            raise SystemExit(f"Missing ported SQL asset: {destination}")
        origin_hash = sha256(origin)
        port_hash = sha256(destination)
        if origin_hash != port_hash:
            raise SystemExit(f"SQL asset differs from reference: {relative.as_posix()}")
        runtime_assets.append(
            {
                "originPath": f"Data/sql/{relative.as_posix()}",
                "originSha256": origin_hash,
                "portPath": destination.relative_to(repo_root).as_posix(),
                "portSha256": port_hash,
            }
        )

    payload = {
        "schema": "aetherxiv.direct-port-source-manifest.v1",
        "policy": (
            "Every source file is included. Exact copies remain immutable; necessary framework "
            "adaptations require an explicit recorded reason and new hash."
        ),
        "entries": entries,
        "runtimeAssets": runtime_assets,
    }
    manifest.parent.mkdir(parents=True, exist_ok=True)
    manifest.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")
    return verify(repo_root, manifest)


def verify(repo_root: Path, manifest: Path) -> int:
    payload = json.loads(manifest.read_text(encoding="utf-8"))
    entries = payload.get("entries", [])
    runtime_assets = payload.get("runtimeAssets", [])
    expected_paths = {entry["portPath"] for entry in entries}
    failures: list[str] = []

    for entry in entries:
        path = repo_root / entry["portPath"]
        if not path.is_file():
            failures.append(f"missing: {entry['portPath']}")
        elif sha256(path) != entry["portSha256"]:
            failures.append(f"hash mismatch: {entry['portPath']}")

    for asset in runtime_assets:
        path = repo_root / asset["portPath"]
        if not path.is_file():
            failures.append(f"missing runtime asset: {asset['portPath']}")
        elif sha256(path) != asset["portSha256"]:
            failures.append(f"runtime asset hash mismatch: {asset['portPath']}")

    actual_paths: set[str] = set()
    for _, _, port_dir in COMPONENTS:
        actual_paths.update(
            path.relative_to(repo_root).as_posix()
            for path in source_files(repo_root / port_dir)
        )
        for source in source_files(repo_root / port_dir):
            if "meteor" in source.read_text(encoding="utf-8-sig").lower():
                failures.append(f"retired product name in source: {source.relative_to(repo_root)}")
        project_files = list((repo_root / port_dir).glob("*.csproj"))
        for project in project_files:
            project_text = project.read_text(encoding="utf-8")
            if "Legacy " in project_text:
                failures.append(f"external reference source dependency: {project.relative_to(repo_root)}")

    for path in sorted(actual_paths - expected_paths):
        failures.append(f"unmanifested: {path}")
    for path in sorted(expected_paths - actual_paths):
        failures.append(f"manifest-only: {path}")

    component_counts: dict[str, int] = {}
    for entry in entries:
        component = entry["originPath"].split("/", 1)[0]
        component_counts[component] = component_counts.get(component, 0) + 1
    if component_counts != EXPECTED_SOURCE_COUNTS:
        failures.append(
            f"source count mismatch: {component_counts}; expected {EXPECTED_SOURCE_COUNTS}"
        )
    if len(runtime_assets) != len(RUNTIME_ASSETS) + 80:
        failures.append(
            f"runtime asset count mismatch: {len(runtime_assets)}; expected {len(RUNTIME_ASSETS) + 80}"
        )

    if failures:
        print("Direct-port source verification failed:")
        for failure in failures:
            print(f"  {failure}")
        return 1

    exact = sum(entry["disposition"] == "exact-copy" for entry in entries)
    adapted = len(entries) - exact
    print(
        f"Direct-port source verified: {len(entries)} C# files "
        f"({exact} exact, {adapted} adapted), {len(runtime_assets)} exact runtime/data assets."
    )
    return 0


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--repo-root", required=True, type=Path)
    parser.add_argument("--manifest", required=True, type=Path)
    parser.add_argument("--capture-reference-root", type=Path)
    args = parser.parse_args()
    if args.capture_reference_root:
        return capture(args.capture_reference_root, args.repo_root, args.manifest)
    return verify(args.repo_root, args.manifest)


if __name__ == "__main__":
    raise SystemExit(main())
