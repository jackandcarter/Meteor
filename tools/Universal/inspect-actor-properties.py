#!/usr/bin/env python3
import argparse
import json
import re
import struct
from pathlib import Path


PROPERTY_OPCODE = "0x0137"

TARGET_PATTERN = re.compile(r"^(?:/|[A-Za-z_])[A-Za-z0-9_./\[\]-]*$")


SCALAR_CANDIDATES = [
    "charaWork.eventSave.bazaarTax",
    "charaWork.battleSave.potencial",
    "charaWork.parameterSave.mp",
    "charaWork.parameterSave.mpMax",
    "charaWork.parameterTemp.tp",
    "charaWork.parameterSave.state_mainSkillLevel",
    "charaWork.commandBorder",
    "charaWork.depictionJudge",
    "playerWork.restBonusExpRate",
    "playerWork.tribe",
    "playerWork.guardian",
    "playerWork.birthdayMonth",
    "playerWork.birthdayDay",
    "playerWork.initialTown",
]


ARRAY_CANDIDATES = {
    "charaWork.property[{index}]": 64,
    "charaWork.parameterSave.hp[{index}]": 8,
    "charaWork.parameterSave.hpMax[{index}]": 8,
    "charaWork.parameterSave.state_mainSkill[{index}]": 8,
    "charaWork.statusShownTime[{index}]": 20,
    "charaWork.battleTemp.generalParameter[{index}]": 256,
    "charaWork.battleTemp.castGauge_speed[{index}]": 8,
    "charaWork.battleSave.skillPoint[{index}]": 64,
    "charaWork.battleSave.skillLevel[{index}]": 64,
    "charaWork.command[{index}]": 256,
    "charaWork.commandCategory[{index}]": 256,
    "charaWork.commandAcquired[{index}]": 2048,
    "charaWork.additionalCommandAcquired[{index}]": 256,
    "charaWork.parameterSave.commandSlot_compatibility[{index}]": 256,
    "charaWork.parameterTemp.maxCommandRecastTime[{index}]": 256,
    "charaWork.parameterSave.commandSlot_recastTime[{index}]": 256,
    "charaWork.parameterTemp.forceControl_float_forClientSelf[{index}]": 16,
    "charaWork.parameterTemp.forceControl_int16_forClientSelf[{index}]": 16,
    "charaWork.parameterTemp.otherClassAbilityCount[{index}]": 64,
    "charaWork.parameterTemp.giftCount[{index}]": 64,
    "charaWork.battleSave.negotiationFlag[{index}]": 16,
    "playerWork.questScenario[{index}]": 64,
    "playerWork.npcLinkshellChatCalling[{index}]": 128,
    "playerWork.npcLinkshellChatExtra[{index}]": 128,
    "work.guildleveId[{index}]": 16,
    "work.guildleveDone[{index}]": 16,
    "work.guildleveChecked[{index}]": 16,
}


def legacy_murmur_hash2(key, seed=0):
    data = key.encode("ascii")
    multiplier = 0x5BD1E995
    length = len(key)
    data_index = length - 4
    result = (seed ^ length) & 0xFFFFFFFF

    while length >= 4:
        result = (result * multiplier) & 0xFFFFFFFF
        value = int.from_bytes(data[data_index : data_index + 4], "little")
        value = (
            ((value >> 24) & 0xFF)
            | ((value << 8) & 0xFF0000)
            | ((value >> 8) & 0xFF00)
            | ((value << 24) & 0xFF000000)
        )
        value = (value * multiplier) & 0xFFFFFFFF
        value ^= value >> 24
        value = (value * multiplier) & 0xFFFFFFFF
        result ^= value
        data_index -= 4
        length -= 4

    if length == 3:
        result ^= data[0] << 16
        result ^= data[1] << 8
        result ^= data[2]
        result = (result * multiplier) & 0xFFFFFFFF
    elif length == 2:
        result ^= data[0] << 8
        result ^= data[1]
        result = (result * multiplier) & 0xFFFFFFFF
    elif length == 1:
        result ^= data[0]
        result = (result * multiplier) & 0xFFFFFFFF

    result ^= result >> 13
    result = (result * multiplier) & 0xFFFFFFFF
    result ^= result >> 15
    return result & 0xFFFFFFFF


def source_candidates(paths):
    candidates = set()
    pattern = re.compile(r"(?:charaWork|playerWork|npcWork)[A-Za-z0-9_./\[\]{}-]*")
    for path in paths:
        text = Path(path).read_text(encoding="utf-8", errors="ignore")
        candidates.update(pattern.findall(text))
    return candidates


def build_hash_names(extra_names, source_paths):
    candidates = set(SCALAR_CANDIDATES)
    candidates.update(extra_names)
    candidates.update(source_candidates(source_paths))
    for template, count in ARRAY_CANDIDATES.items():
        candidates.update(template.format(index=index) for index in range(count))

    names = {}
    for candidate in sorted(candidates):
        names.setdefault(legacy_murmur_hash2(candidate), []).append(candidate)
    return names


def decode_value(raw):
    if len(raw) == 1:
        return raw[0]
    if len(raw) == 2:
        return int.from_bytes(raw, "little")
    if len(raw) == 4:
        integer = int.from_bytes(raw, "little")
        floating = struct.unpack("<f", raw)[0]
        return {"uint32": integer, "float32": floating}
    return raw.hex().upper()


def decode_target_marker(payload, offset, end_offset):
    marker = payload[offset]
    candidates = []
    if marker >= 0xA4:
        candidates.append(("array", marker - 0xA4))
    if marker >= 0x82:
        candidates.append(("final", marker - 0x82))
    if marker >= 0x60:
        candidates.append(("more", marker - 0x60))

    for marker_kind, target_length in candidates:
        if target_length == 0 or offset + 1 + target_length > end_offset:
            continue
        raw_target = payload[offset + 1 : offset + 1 + target_length]
        try:
            target = raw_target.decode("ascii")
        except UnicodeDecodeError:
            continue
        if TARGET_PATTERN.fullmatch(target):
            return {
                "kind": "target",
                "markerKind": marker_kind,
                "target": target,
                "size": 1 + target_length,
            }
    return None


def decode_property_payload(payload_hex, hash_names):
    payload = bytes.fromhex(payload_hex)
    used_bytes = payload[0]
    end_offset = used_bytes + 1
    if used_bytes == 0 or end_offset > len(payload):
        raise ValueError(f"Invalid actor-property used byte count {used_bytes}.")

    tokens = []
    offset = 1
    while offset < end_offset:
        target_marker = decode_target_marker(payload, offset, end_offset)
        if target_marker is not None:
            tokens.append(target_marker)
            offset += target_marker["size"]
            continue

        size = payload[offset]
        if offset + 5 + size > end_offset:
            raise ValueError(
                f"Unrecognized actor-property token 0x{payload[offset]:02X} "
                f"at payload offset {offset}; {end_offset - offset} used bytes remain."
            )
        property_id = int.from_bytes(payload[offset + 1 : offset + 5], "little")
        raw = payload[offset + 5 : offset + 5 + size]
        tokens.append(
            {
                "kind": "value",
                "propertyId": f"0x{property_id:08X}",
                "names": hash_names.get(property_id, []),
                "size": size,
                "value": decode_value(raw),
                "valueHex": raw.hex().upper(),
            }
        )
        offset += 5 + size

    return tokens


def inspect(document, hash_names, direction_filter, target_filter):
    rows = []
    unresolved = []
    for direction in document.get("directions", []):
        direction_name = direction.get("direction")
        if direction_filter and direction_name != direction_filter:
            continue
        pending_by_actor = {}
        for frame_index, frame in enumerate(direction.get("frames", [])):
            for subpacket_index, subpacket in enumerate(frame.get("subpackets", [])):
                if subpacket.get("opcode") != PROPERTY_OPCODE:
                    continue
                actor_key = (
                    subpacket.get("sourceActorId"),
                    subpacket.get("targetActorId"),
                )
                pending = pending_by_actor.setdefault(actor_key, [])
                packet_location = {
                    "frameIndex": frame_index,
                    "subPacketIndex": subpacket_index,
                }
                for token in decode_property_payload(subpacket["payloadHex"], hash_names):
                    if token["kind"] == "value":
                        value = dict(token)
                        value.pop("kind")
                        value["frameIndex"] = frame_index
                        value["subPacketIndex"] = subpacket_index
                        pending.append(value)
                        continue

                    target = token["target"]
                    if pending and (not target_filter or target == target_filter):
                        rows.append(
                            {
                                "direction": direction_name,
                                "startFrameIndex": pending[0]["frameIndex"],
                                "startSubPacketIndex": pending[0]["subPacketIndex"],
                                "endFrameIndex": frame_index,
                                "endSubPacketIndex": subpacket_index,
                                "sourceActorId": subpacket.get("sourceActorId"),
                                "targetActorId": subpacket.get("targetActorId"),
                                "target": target,
                                "markerKind": token["markerKind"],
                                "values": pending.copy(),
                            }
                        )
                    pending.clear()

        for (source_actor_id, target_actor_id), pending in pending_by_actor.items():
            if not pending:
                continue
            unresolved.append(
                {
                    "direction": direction_name,
                    "sourceActorId": source_actor_id,
                    "targetActorId": target_actor_id,
                    "startFrameIndex": pending[0]["frameIndex"],
                    "startSubPacketIndex": pending[0]["subPacketIndex"],
                    "endFrameIndex": pending[-1]["frameIndex"],
                    "endSubPacketIndex": pending[-1]["subPacketIndex"],
                    "reason": "Capture ended before the postfix target marker was observed.",
                    "values": pending,
                }
            )
    return rows, unresolved


def main():
    parser = argparse.ArgumentParser(
        description="Inspect and resolve SetActorProperty entries from analyze-legacy-tcp-stream JSON."
    )
    parser.add_argument("stream_json", help="JSON emitted by analyze-legacy-tcp-stream.py")
    parser.add_argument("--direction", choices=["server-to-client", "client-to-server"])
    parser.add_argument("--target", help="only emit packets for this exact actor-property target")
    parser.add_argument("--name", action="append", default=[], help="additional exact property name candidate")
    parser.add_argument(
        "--names-from",
        action="append",
        default=[],
        help="extract actor-property name candidates from a source file",
    )
    parser.add_argument("--out", help="output JSON path; stdout when omitted")
    args = parser.parse_args()

    document = json.loads(Path(args.stream_json).read_text(encoding="utf-8"))
    hash_names = build_hash_names(args.name, args.names_from)
    packets, unresolved = inspect(document, hash_names, args.direction, args.target)
    result = {
        "schema": "aetherxiv.trace.actor-properties.v2",
        "source": str(Path(args.stream_json)),
        "capture": document.get("capture"),
        "tcpStream": document.get("tcpStream"),
        "evidenceStatus": "TraceConfirmed",
        "packets": packets,
        "unresolvedSequences": unresolved,
    }
    output = json.dumps(result, indent=2)
    if args.out:
        Path(args.out).write_text(output + "\n", encoding="utf-8")
    else:
        print(output)


if __name__ == "__main__":
    main()
