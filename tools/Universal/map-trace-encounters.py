#!/usr/bin/env python3
"""Extract reviewable encounter observations from a decoded official TCP stream.

This tool deliberately emits evidence, not SQL or authoritative spawn rows. Promotion
into game data remains a separate reviewed step so a short capture cannot become an
invented global population, patrol, respawn timer, or drop-rate rule.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import math
import struct
from collections import Counter
from pathlib import Path


SCHEMA = "aetherxiv.trace.encounter-observations.v2"
LEGACY_NPC_ACTOR_KIND = 4


def u32(data: bytes, offset: int = 0) -> int:
    return struct.unpack_from("<I", data, offset)[0]


def floats(data: bytes, offset: int) -> tuple[float, float, float, float]:
    return struct.unpack_from("<ffff", data, offset)


def c_string(data: bytes, start: int, length: int) -> str:
    return data[start : start + length].split(b"\0", 1)[0].decode("ascii", errors="replace")


def point(values: tuple[float, float, float, float]) -> dict[str, float]:
    return dict(zip(("x", "y", "z", "rotation"), values))


def distance(a: dict[str, float], b: dict[str, float]) -> float:
    return math.sqrt(sum((a[key] - b[key]) ** 2 for key in ("x", "y", "z")))


def decode_lua_parameters(data: bytes) -> list[dict]:
    """Decode the 1.x typed Lua parameter tail used by actor-init 0x00CC."""
    parameters: list[dict] = []
    offset = 0
    while offset < len(data):
        parameter_offset = offset
        code = data[offset]
        offset += 1
        if code == 0x0F:
            break
        value: object = None
        kind = "unknown"
        try:
            if code == 0x00:
                kind, value = "int32", struct.unpack_from(">i", data, offset)[0]
                offset += 4
            elif code == 0x01:
                kind, value = "uint32", struct.unpack_from(">I", data, offset)[0]
                offset += 4
            elif code == 0x02:
                end = data.index(0, offset)
                kind, value = "string", data[offset:end].decode("ascii", errors="replace")
                offset = end + 1
            elif code == 0x03:
                kind, value = "boolean", True
            elif code == 0x04:
                kind, value = "boolean", False
            elif code == 0x05:
                kind, value = "nil", None
            elif code == 0x06:
                kind, value = "actor-id", struct.unpack_from(">I", data, offset)[0]
                offset += 4
            elif code == 0x07:
                actor_id = struct.unpack_from(">I", data, offset)[0]
                value = {
                    "actorId": actor_id,
                    "unknown": data[offset + 4],
                    "slot": data[offset + 5],
                    "itemPackage": data[offset + 6],
                }
                kind = "item-reference"
                offset += 7
            elif code == 0x08:
                kind, value = "item-offer", data[offset : offset + 12].hex()
                offset += 12
            elif code == 0x09:
                kind, value = "uint64-pair", [
                    struct.unpack_from("<Q", data, offset)[0],
                    struct.unpack_from("<Q", data, offset + 8)[0],
                ]
                offset += 16
            elif code == 0x0C:
                kind, value = "byte", data[offset]
                offset += 1
            elif code == 0x1B:
                kind, value = "uint16", struct.unpack_from("<H", data, offset)[0]
                offset += 2
            else:
                parameters.append(
                    {"index": len(parameters), "offset": parameter_offset, "code": f"0x{code:02X}", "kind": kind}
                )
                break
        except (IndexError, ValueError, struct.error):
            parameters.append(
                {
                    "index": len(parameters),
                    "offset": parameter_offset,
                    "code": f"0x{code:02X}",
                    "kind": "truncated",
                }
            )
            break
        parameters.append(
            {
                "index": len(parameters),
                "offset": parameter_offset,
                "code": f"0x{code:02X}",
                "kind": kind,
                "value": value,
            }
        )
    return parameters


def actor_init_evidence(payload: bytes) -> dict:
    parameters = decode_lua_parameters(payload[68:]) if len(payload) > 68 else []
    class_path = next(
        (
            item["value"]
            for item in parameters
            if item.get("kind") == "string" and str(item.get("value", "")).startswith("/Chara/")
        ),
        None,
    )
    actor_class_id = None
    if len(parameters) > 6 and parameters[6].get("kind") in ("int32", "uint32"):
        candidate = int(parameters[6]["value"])
        if 1_000_000 <= candidate <= 99_999_999:
            actor_class_id = candidate
    if actor_class_id is None:
        actor_class_id = next(
            (
                int(item["value"])
                for item in parameters
                if item.get("kind") in ("int32", "uint32")
                and 1_000_000 <= int(item["value"]) <= 99_999_999
            ),
            None,
        )
    return {"classPath": class_path, "actorClassId": actor_class_id, "parameters": parameters}


def actor_classification(identity: dict | None) -> str:
    if not identity:
        return "unresolved"
    combined = f"{identity['objectName']} {identity['className']} {identity.get('classPath') or ''}".lower()
    if "/director/" in combined or "director" in combined:
        return "director-candidate"
    if "/populace/" in combined:
        return "static-populace-candidate"
    if any(marker in combined for marker in ("monster", "battle", "enemy", "lesser")):
        return "battle-npc-candidate"
    if "player" in combined:
        return "player-candidate"
    return "actor"


def actor_zone_id(actor_id: int) -> int | None:
    """Decode the zone field used by retail/legacy non-player actor IDs."""
    if actor_id >> 28 != LEGACY_NPC_ACTOR_KIND:
        return None
    return (actor_id >> 19) & 0x1FF


def infer_zone_id(events: list[dict]) -> tuple[int | None, dict[int, int]]:
    candidates: Counter[int] = Counter()
    seen: set[int] = set()
    for event in events:
        packet = event["packet"]
        if event["direction"] != "server-to-client" or packet["opcode"] not in ("0x00CC", "0x00CE", "0x00CF"):
            continue
        actor_id = packet["sourceActorId"]
        if actor_id in seen:
            continue
        seen.add(actor_id)
        zone_id = actor_zone_id(actor_id)
        if zone_id is not None:
            candidates[zone_id] += 1
    if not candidates:
        return None, {}
    winner, count = candidates.most_common(1)[0]
    if len(candidates) > 1 and count == candidates.most_common(2)[1][1]:
        return None, dict(sorted(candidates.items()))
    return winner, dict(sorted(candidates.items()))


def flatten(document: dict) -> list[dict]:
    events: list[dict] = []
    for direction_index, direction in enumerate(document["directions"]):
        for frame_index, frame in enumerate(direction["frames"]):
            for packet_index, packet in enumerate(frame["subpackets"]):
                events.append(
                    {
                        "timestamp": frame["timestamp"],
                        "frameNumber": frame.get("frameNumber"),
                        "captureTimestamp": frame.get("captureTimestamp"),
                        "direction": direction["direction"],
                        "directionIndex": direction_index,
                        "frameIndex": frame_index,
                        "packetIndex": packet_index,
                        "packet": packet,
                    }
                )
    events.sort(key=lambda item: (item["timestamp"], item["directionIndex"], item["frameIndex"], item["packetIndex"]))
    return events


def map_observations(
    document: dict,
    zone_id: int | None,
    zone_evidence: str | None,
    client_build: str = "2012.09.19.0001",
    capture_sha256: str | None = None,
) -> dict:
    identities: dict[int, dict] = {}
    actor_positions: dict[int, dict] = {}
    player_positions: dict[int, dict] = {}
    interactions: list[dict] = []
    seen: set[tuple] = set()

    events = flatten(document)
    inferred_zone_id, zone_candidates = infer_zone_id(events)
    if zone_id is None:
        if inferred_zone_id is None:
            raise ValueError(f"Zone could not be inferred unambiguously from actor IDs: {zone_candidates}")
        zone_id = inferred_zone_id
    elif inferred_zone_id is not None and zone_id != inferred_zone_id:
        raise ValueError(
            f"Explicit zone {zone_id} contradicts actor-ID consensus zone {inferred_zone_id}: {zone_candidates}"
        )
    if not zone_evidence:
        zone_evidence = f"retail NPC actor-ID consensus {zone_candidates}"

    for event in events:
        packet = event["packet"]
        opcode = packet["opcode"]
        payload = bytes.fromhex(packet["payloadHex"])
        actor_id = packet["sourceActorId"]
        dedupe = (event["timestamp"], event["direction"], opcode, actor_id, packet["payloadHex"])
        if dedupe in seen:
            continue
        seen.add(dedupe)

        if event["direction"] == "server-to-client" and opcode == "0x00CC" and len(payload) >= 68:
            init = actor_init_evidence(payload)
            identities[actor_id] = {
                "objectName": c_string(payload, 4, 32),
                "className": c_string(payload, 36, 32),
                "actorClassId": init["actorClassId"],
                "classPath": init["classPath"],
                "actorInitParameters": init["parameters"],
                "source": {
                    "frameIndex": event["frameIndex"],
                    "frameNumber": event["frameNumber"],
                    "captureTimestamp": event["captureTimestamp"],
                    "packetIndex": event["packetIndex"],
                    "timestamp": event["timestamp"],
                    "opcode": opcode,
                },
            }
        elif event["direction"] == "server-to-client" and opcode in ("0x00CE", "0x00CF") and len(payload) >= 24:
            actor_positions[actor_id] = {
                "timestamp": event["timestamp"],
                "position": point(floats(payload, 8)),
                "sourceOpcode": opcode,
                "frameIndex": event["frameIndex"],
                "frameNumber": event["frameNumber"],
                "captureTimestamp": event["captureTimestamp"],
                "packetIndex": event["packetIndex"],
            }
        elif event["direction"] == "client-to-server" and opcode == "0x00CA" and len(payload) >= 24:
            player_positions[actor_id] = {
                "timestamp": event["timestamp"],
                "position": point(floats(payload, 8)),
            }
        elif event["direction"] == "client-to-server" and opcode == "0x00CD" and len(payload) >= 8:
            target_id = u32(payload)
            attack_target = u32(payload, 4)
            player = player_positions.get(actor_id)
            target = actor_positions.get(target_id)
            identity = identities.get(target_id)
            confidence = "high" if player and target and identity else "medium" if player and target else "low"
            observation = {
                "kind": "target-interaction",
                "timestamp": event["timestamp"],
                "zoneId": zone_id,
                "zoneEvidence": zone_evidence,
                "playerActorId": actor_id,
                "targetActorId": target_id,
                "attackTarget": attack_target,
                "interactionSource": {
                    "frameNumber": event["frameNumber"],
                    "streamFrameIndex": event["frameIndex"],
                    "packetIndex": event["packetIndex"],
                    "captureTimestamp": event["captureTimestamp"],
                },
                "identity": identity,
                "classification": actor_classification(identity),
                "playerPosition": player["position"] if player else None,
                "targetPosition": target["position"] if target else None,
                "targetPositionOpcode": target["sourceOpcode"] if target else None,
                "targetPositionSource": (
                    {
                        "frameNumber": target["frameNumber"],
                        "streamFrameIndex": target["frameIndex"],
                        "packetIndex": target["packetIndex"],
                        "captureTimestamp": target["captureTimestamp"],
                    }
                    if target
                    else None
                ),
                "distance": round(distance(player["position"], target["position"]), 4) if player and target else None,
                "confidence": confidence,
                "evidenceStatus": "observation-only",
            }
            interactions.append(observation)

    visible = []
    interacted_ids = {item["targetActorId"] for item in interactions}
    for actor_id, position in sorted(actor_positions.items()):
        identity = identities.get(actor_id)
        if actor_id in interacted_ids or identity is None:
            continue
        visible.append(
            {
                "kind": "visibility-observation",
                "timestamp": position["timestamp"],
                "zoneId": zone_id,
                "zoneEvidence": zone_evidence,
                "targetActorId": actor_id,
                "identity": identity,
                "classification": actor_classification(identity),
                "targetPosition": position["position"],
                "targetPositionOpcode": position["sourceOpcode"],
                "targetPositionSource": {
                    "frameNumber": position["frameNumber"],
                    "streamFrameIndex": position["frameIndex"],
                    "packetIndex": position["packetIndex"],
                    "captureTimestamp": position["captureTimestamp"],
                },
                "confidence": "medium",
                "evidenceStatus": "observation-only",
            }
        )

    return {
        "schema": SCHEMA,
        "source": {
            "capture": document.get("capture"),
            "tcpStream": document.get("tcpStream"),
            "sourceSchema": document.get("schema"),
            "captureSha256": capture_sha256,
            "captureStart": document.get("captureStart"),
            "clientBuild": client_build,
        },
        "scope": {
            "zoneId": zone_id,
            "zoneEvidence": zone_evidence,
            "actorIdZoneCandidates": zone_candidates,
        },
        "limitations": [
            "Observations are capture-local and are not authoritative global spawn rows.",
            "Visibility does not prove a home position, patrol boundary, respawn timer, probability, or drop rate.",
            "Actor-ID zone consensus proves the public zone field but does not by itself identify a private-area/content instance.",
            "Actor initialization is decoded for review only; this tool never emits SQL or production spawn rows.",
        ],
        "interactions": interactions,
        "visibleActors": visible,
    }


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("decoded_stream", type=Path, help="JSON emitted by analyze-legacy-tcp-stream.py")
    parser.add_argument("--zone-id", type=int, help="optional independently proven zone id; contradictions abort")
    parser.add_argument("--zone-evidence", help="human-readable provenance for an explicit zone assignment")
    parser.add_argument("--client-build", default="2012.09.19.0001")
    parser.add_argument("--capture-sha256", help="known SHA-256 of the source pcap")
    parser.add_argument("--out", type=Path)
    args = parser.parse_args()
    document = json.loads(args.decoded_stream.read_text(encoding="utf-8"))
    if document.get("schema") != "aetherxiv.trace.tcp-stream.v1":
        raise SystemExit(f"Unsupported decoded stream schema: {document.get('schema')}")
    capture_sha256 = args.capture_sha256 or document.get("captureSha256")
    capture = document.get("capture")
    if capture_sha256 is None and capture:
        capture_path = Path(capture)
        if capture_path.is_file():
            capture_sha256 = hashlib.sha256(capture_path.read_bytes()).hexdigest()
    result = map_observations(document, args.zone_id, args.zone_evidence, args.client_build, capture_sha256)
    encoded = json.dumps(result, indent=2) + "\n"
    if args.out:
        args.out.parent.mkdir(parents=True, exist_ok=True)
        args.out.write_text(encoded, encoding="utf-8")
    else:
        print(encoded, end="")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
