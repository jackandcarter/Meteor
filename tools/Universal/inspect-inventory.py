#!/usr/bin/env python3
"""Reconstruct legacy item-package and equipment-link state from decoded TCP streams."""

import argparse
import json
import struct
from pathlib import Path


ITEM_LIST_COUNTS = {
    "0x0148": 1,
    "0x0149": 8,
    "0x014A": 16,
    "0x014B": 32,
    "0x014C": 64,
}

LINK_LIST_COUNTS = {
    "0x014D": 1,
    "0x014E": 8,
    "0x014F": 16,
    "0x0150": 32,
    "0x0151": 64,
}

REMOVE_COUNTS = {
    "0x0152": 1,
    "0x0153": 8,
    "0x0154": 16,
    "0x0155": 32,
    "0x0156": 64,
}

ITEM_RECORD_SIZE = 0x70
EQUIPMENT_PACKAGE = 0x00FE


def u16(data, offset):
    return struct.unpack_from("<H", data, offset)[0]


def u32(data, offset):
    return struct.unpack_from("<I", data, offset)[0]


def u64(data, offset):
    return struct.unpack_from("<Q", data, offset)[0]


def parse_item(data):
    if len(data) < ITEM_RECORD_SIZE:
        raise ValueError(f"Item record is {len(data)} bytes; expected {ITEM_RECORD_SIZE}.")
    has_modifiers = data[41] != 0
    item = {
        "uniqueId": u64(data, 0),
        "quantity": struct.unpack_from("<i", data, 8)[0],
        "itemId": u32(data, 12),
        "slot": u16(data, 16),
        "quality": data[40],
        "hasModifiers": has_modifiers,
    }
    if has_modifiers:
        item["modifiers"] = {
            "durability": u32(data, 42),
            "use": u16(data, 46),
            "materiaId": u32(data, 48),
            "materiaLife": u32(data, 52),
            "mainQuality": data[56],
            "subQuality": list(data[57:60]),
            "polish": u32(data, 60),
            "parameters": [u32(data, 64), u32(data, 68), u32(data, 72)],
            "spiritbind": u16(data, 76),
            "materia": [
                {"type": materia_type, "grade": materia_grade}
                for materia_type, materia_grade in zip(data[78:83], data[83:88])
                if materia_type or materia_grade
            ],
        }
    return item


def nonempty_item_records(payload, maximum):
    count = maximum
    if maximum == 8 and len(payload) >= 0x384:
        count = min(u32(payload, 0x380), maximum)
    records = []
    for index in range(count):
        start = index * ITEM_RECORD_SIZE
        end = start + ITEM_RECORD_SIZE
        if end > len(payload):
            break
        item = parse_item(payload[start:end])
        if item["uniqueId"] or item["itemId"]:
            records.append(item)
    return records


def nonempty_link_records(payload, maximum):
    count = maximum
    if maximum == 8 and len(payload) >= 0x34:
        count = min(u32(payload, 0x30), maximum)
    records = []
    for index in range(count):
        start = index * 6
        if start + 6 > len(payload):
            break
        position, source_slot, source_package = struct.unpack_from("<HHH", payload, start)
        if position or source_slot or source_package:
            records.append(
                {
                    "position": position,
                    "sourceSlot": source_slot,
                    "sourcePackage": source_package,
                }
            )
    return records


def nonempty_remove_records(payload, maximum):
    count = maximum
    if maximum == 8 and len(payload) >= 0x11:
        count = min(payload[0x10], maximum)
    records = []
    for index in range(count):
        start = index * 2
        if start + 2 > len(payload):
            break
        records.append(u16(payload, start))
    return records


def inspect(document, actor_filter):
    packages_by_actor = {}
    links_by_actor = {}
    current_set_by_actor = {}
    operations = []

    for direction in document.get("directions", []):
        if direction.get("direction") != "server-to-client":
            continue
        for frame_index, frame in enumerate(direction.get("frames", [])):
            for subpacket_index, subpacket in enumerate(frame.get("subpackets", [])):
                opcode = subpacket.get("opcode")
                actor_id = subpacket.get("sourceActorId")
                if actor_filter is not None and actor_id != actor_filter:
                    continue
                if actor_id is None:
                    continue
                payload = bytes.fromhex(subpacket.get("payloadHex", ""))
                location = {
                    "frameIndex": frame_index,
                    "subPacketIndex": subpacket_index,
                    "actorId": actor_id,
                    "opcode": opcode,
                }

                if opcode == "0x0146":
                    if len(payload) < 8:
                        raise ValueError(f"Truncated InventorySetBegin at frame {frame_index}.")
                    owner_id, capacity, package_code = struct.unpack_from("<IHH", payload, 0)
                    current_set_by_actor[actor_id] = {
                        "ownerActorId": owner_id,
                        "capacity": capacity,
                        "packageCode": package_code,
                    }
                    operations.append({**location, "kind": "begin", **current_set_by_actor[actor_id]})
                    continue

                if opcode == "0x0147":
                    current = current_set_by_actor.pop(actor_id, None)
                    operations.append({**location, "kind": "end", "set": current})
                    continue

                current = current_set_by_actor.get(actor_id)
                if current is None:
                    continue
                package_code = current["packageCode"]

                if opcode in ITEM_LIST_COUNTS:
                    items = nonempty_item_records(payload, ITEM_LIST_COUNTS[opcode])
                    package = packages_by_actor.setdefault(actor_id, {}).setdefault(package_code, {})
                    for item in items:
                        package[item["slot"]] = item
                    operations.append(
                        {**location, "kind": "items", "packageCode": package_code, "items": items}
                    )
                    continue

                if opcode in LINK_LIST_COUNTS:
                    links = nonempty_link_records(payload, LINK_LIST_COUNTS[opcode])
                    package_links = links_by_actor.setdefault(actor_id, {}).setdefault(package_code, {})
                    for link in links:
                        package_links[link["position"]] = {
                            "sourcePackage": link["sourcePackage"],
                            "sourceSlot": link["sourceSlot"],
                        }
                    operations.append(
                        {**location, "kind": "links", "packageCode": package_code, "links": links}
                    )
                    continue

                if opcode in REMOVE_COUNTS:
                    positions = nonempty_remove_records(payload, REMOVE_COUNTS[opcode])
                    for position in positions:
                        links_by_actor.setdefault(actor_id, {}).setdefault(package_code, {}).pop(position, None)
                        packages_by_actor.setdefault(actor_id, {}).setdefault(package_code, {}).pop(position, None)
                    operations.append(
                        {
                            **location,
                            "kind": "remove",
                            "packageCode": package_code,
                            "positions": positions,
                        }
                    )

    actors = []
    actor_ids = sorted(set(packages_by_actor) | set(links_by_actor))
    for actor_id in actor_ids:
        package_state = packages_by_actor.get(actor_id, {})
        link_state = links_by_actor.get(actor_id, {})
        equipment = []
        for position, link in sorted(link_state.get(EQUIPMENT_PACKAGE, {}).items()):
            item = package_state.get(link["sourcePackage"], {}).get(link["sourceSlot"])
            equipment.append({"position": position, **link, "item": item})
        actors.append(
            {
                "actorId": actor_id,
                "packages": {
                    str(code): [package[slot] for slot in sorted(package)]
                    for code, package in sorted(package_state.items())
                },
                "links": {
                    str(code): [
                        {"position": position, **link}
                        for position, link in sorted(package.items())
                    ]
                    for code, package in sorted(link_state.items())
                },
                "equipment": equipment,
            }
        )

    return operations, actors, current_set_by_actor


def main():
    parser = argparse.ArgumentParser(
        description="Reconstruct item packages and linked equipment from analyze-legacy-tcp-stream JSON."
    )
    parser.add_argument("stream_json", help="JSON emitted by analyze-legacy-tcp-stream.py")
    parser.add_argument("--actor", type=lambda value: int(value, 0), help="source actor ID filter")
    parser.add_argument("--out", help="output JSON path; stdout when omitted")
    args = parser.parse_args()

    document = json.loads(Path(args.stream_json).read_text(encoding="utf-8"))
    operations, actors, incomplete_sets = inspect(document, args.actor)
    result = {
        "schema": "aetherxiv.trace.inventory.v1",
        "source": str(Path(args.stream_json)),
        "capture": document.get("capture"),
        "tcpStream": document.get("tcpStream"),
        "evidenceStatus": "TraceConfirmed",
        "operations": operations,
        "actors": actors,
        "incompleteSets": incomplete_sets,
    }
    output = json.dumps(result, indent=2)
    if args.out:
        Path(args.out).write_text(output + "\n", encoding="utf-8")
    else:
        print(output)


if __name__ == "__main__":
    main()
