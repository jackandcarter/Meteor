#!/usr/bin/env python3
import argparse
import datetime
import hashlib
import json
from pathlib import Path
import subprocess
import zlib


BASE_HEADER_SIZE = 0x10
SUBPACKET_HEADER_SIZE = 0x10
GAME_HEADER_SIZE = 0x10


def read_follow_stream(capture, stream_index):
    completed = subprocess.run(
        [
            "tshark",
            "-r",
            capture,
            "-q",
            "-z",
            f"follow,tcp,raw,{stream_index}",
        ],
        check=True,
        text=True,
        capture_output=True,
    )

    endpoints = []
    streams = [bytearray(), bytearray()]
    for line in completed.stdout.splitlines():
        if line.startswith("Node 0:") or line.startswith("Node 1:"):
            endpoints.append(line.split(": ", 1)[1])
            continue

        direction = 1 if line.startswith("\t") else 0
        payload = line.strip()
        if not payload or any(character not in "0123456789abcdefABCDEF" for character in payload):
            continue
        streams[direction].extend(bytes.fromhex(payload))

    if len(endpoints) != 2:
        raise RuntimeError(f"Could not resolve both endpoints for TCP stream {stream_index}.")
    return endpoints, streams


def read_capture_segments(capture, stream_index, endpoints):
    """Map reassembled stream offsets back to their first capture frame."""
    completed = subprocess.run(
        [
            "tshark",
            "-r",
            capture,
            "-Y",
            f"tcp.stream == {stream_index} && tcp.len > 0",
            "-T",
            "fields",
            "-e",
            "frame.number",
            "-e",
            "frame.time_epoch",
            "-e",
            "tcp.srcport",
            "-e",
            "tcp.seq",
            "-e",
            "tcp.payload",
            "-E",
            "separator=|",
            "-E",
            "occurrence=f",
            "-o",
            "tcp.relative_sequence_numbers:TRUE",
        ],
        check=True,
        text=True,
        capture_output=True,
    )

    endpoint_ports = [int(endpoint.rsplit(":", 1)[1]) for endpoint in endpoints]
    segments = [[], []]
    for line in completed.stdout.splitlines():
        fields = line.split("|", 4)
        if len(fields) != 5 or not fields[0] or not fields[2] or not fields[3] or not fields[4]:
            continue
        frame_number, timestamp, source_port, sequence, payload = fields
        try:
            direction_index = endpoint_ports.index(int(source_port))
            payload_length = len(bytes.fromhex(payload.replace(":", "")))
            segments[direction_index].append(
                {
                    "frameNumber": int(frame_number),
                    "captureTimestamp": timestamp,
                    "sequence": int(sequence),
                    "payloadLength": payload_length,
                }
            )
        except (ValueError, IndexError):
            continue

    for direction_segments in segments:
        if not direction_segments:
            continue
        base_sequence = min(segment["sequence"] for segment in direction_segments)
        for segment in direction_segments:
            segment["streamOffset"] = segment.pop("sequence") - base_sequence
        direction_segments.sort(key=lambda segment: (segment["streamOffset"], segment["frameNumber"]))
    return segments


def decode_subpackets(body):
    packets = []
    offset = 0
    while offset < len(body):
        if offset + SUBPACKET_HEADER_SIZE > len(body):
            raise ValueError(f"Subpacket header is truncated at body offset {offset}.")

        packet_size = int.from_bytes(body[offset : offset + 2], "little")
        packet_type = int.from_bytes(body[offset + 2 : offset + 4], "little")
        if packet_size < SUBPACKET_HEADER_SIZE or offset + packet_size > len(body):
            raise ValueError(f"Invalid subpacket size {packet_size} at body offset {offset}.")

        source_actor_id = int.from_bytes(body[offset + 4 : offset + 8], "little")
        target_actor_id = int.from_bytes(body[offset + 8 : offset + 12], "little")
        header_unknown = int.from_bytes(body[offset + 12 : offset + 16], "little")
        opcode = packet_type
        payload_offset = offset + SUBPACKET_HEADER_SIZE
        game_unknown_5 = None
        game_timestamp = None
        game_unknown_6 = None
        if packet_type == 0x0003:
            if packet_size < SUBPACKET_HEADER_SIZE + GAME_HEADER_SIZE:
                raise ValueError(f"Game subpacket is truncated at body offset {offset}.")
            marker = int.from_bytes(body[offset + 16 : offset + 18], "little")
            if marker != 0x0014:
                raise ValueError(f"Unexpected game marker 0x{marker:04X} at body offset {offset}.")
            opcode = int.from_bytes(body[offset + 18 : offset + 20], "little")
            game_unknown_5 = int.from_bytes(body[offset + 20 : offset + 24], "little")
            game_timestamp = int.from_bytes(body[offset + 24 : offset + 28], "little")
            game_unknown_6 = int.from_bytes(body[offset + 28 : offset + 32], "little")
            payload_offset += GAME_HEADER_SIZE

        packets.append(
            {
                "offset": offset,
                "size": packet_size,
                "type": f"0x{packet_type:04X}",
                "opcode": f"0x{opcode:04X}",
                "sourceActorId": source_actor_id,
                "targetActorId": target_actor_id,
                "headerUnknown": header_unknown,
                "gameUnknown5": game_unknown_5,
                "gameTimestamp": game_timestamp,
                "gameUnknown6": game_unknown_6,
                "payloadHex": body[payload_offset : offset + packet_size].hex().upper(),
            }
        )
        offset += packet_size
    return packets


def locate_capture_frame(segments, stream_offset):
    candidates = [
        segment
        for segment in segments or []
        if segment["streamOffset"] <= stream_offset
        < segment["streamOffset"] + segment["payloadLength"]
    ]
    return min(candidates, key=lambda segment: segment["frameNumber"]) if candidates else None


def decode_frames(stream, segments=None):
    frames = []
    offset = 0
    while offset < len(stream):
        if offset + BASE_HEADER_SIZE > len(stream):
            raise ValueError(f"Base frame header is truncated at stream offset {offset}.")

        packet_size = int.from_bytes(stream[offset + 4 : offset + 6], "little")
        subpacket_count = int.from_bytes(stream[offset + 6 : offset + 8], "little")
        if packet_size < BASE_HEADER_SIZE or offset + packet_size > len(stream):
            raise ValueError(f"Invalid base frame size {packet_size} at stream offset {offset}.")

        encoded_body = bytes(stream[offset + BASE_HEADER_SIZE : offset + packet_size])
        compressed = stream[offset + 1] != 0
        body = zlib.decompress(encoded_body) if compressed else encoded_body
        subpackets = decode_subpackets(body)
        if len(subpackets) != subpacket_count:
            raise ValueError(
                f"Base frame at offset {offset} declares {subpacket_count} subpackets "
                f"but contains {len(subpackets)}."
            )

        capture_frame = locate_capture_frame(segments, offset)
        decoded = {
                "streamOffset": offset,
                "authenticated": stream[offset] != 0,
                "compressed": compressed,
                "connectionType": int.from_bytes(stream[offset + 2 : offset + 4], "little"),
                "packetSize": packet_size,
                "subpacketCount": subpacket_count,
                "timestamp": int.from_bytes(stream[offset + 8 : offset + 16], "little"),
                "uncompressedBodySize": len(body),
                "subpackets": subpackets,
            }
        if capture_frame is not None:
            decoded["frameNumber"] = capture_frame["frameNumber"]
            decoded["captureTimestamp"] = capture_frame["captureTimestamp"]
        frames.append(decoded)
        offset += packet_size
    return frames


def main():
    parser = argparse.ArgumentParser(
        description="Reassemble and decode both directions of one legacy FFXIV TCP stream."
    )
    parser.add_argument("capture", help="pcap or pcapng capture path")
    parser.add_argument("--stream", type=int, required=True, help="tshark tcp.stream index")
    parser.add_argument("--server-port", type=int, default=54992, help="server port used for direction labels")
    parser.add_argument("--out", help="output JSON path; stdout when omitted")
    args = parser.parse_args()

    endpoints, streams = read_follow_stream(args.capture, args.stream)
    segments = read_capture_segments(args.capture, args.stream, endpoints)
    directions = []
    for endpoint, stream in zip(endpoints, streams):
        endpoint_port = int(endpoint.rsplit(":", 1)[1])
        direction = "server-to-client" if endpoint_port == args.server_port else "client-to-server"
        directions.append(
            {
                "source": endpoint,
                "direction": direction,
                "streamBytes": len(stream),
                "frames": decode_frames(stream, segments[len(directions)]),
            }
        )

    capture_timestamps = [
        float(segment["captureTimestamp"])
        for direction_segments in segments
        for segment in direction_segments
    ]
    capture_path = Path(args.capture)
    result = {
        "schema": "aetherxiv.trace.tcp-stream.v1",
        "capture": args.capture,
        "captureSha256": hashlib.sha256(capture_path.read_bytes()).hexdigest(),
        "captureStart": (
            datetime.datetime.fromtimestamp(min(capture_timestamps), datetime.timezone.utc)
            .isoformat()
            .replace("+00:00", "Z")
            if capture_timestamps
            else None
        ),
        "tcpStream": args.stream,
        "serverPort": args.server_port,
        "directions": directions,
    }
    output = json.dumps(result, indent=2)
    if args.out:
        with open(args.out, "w", encoding="utf-8") as output_file:
            output_file.write(output + "\n")
    else:
        print(output)


if __name__ == "__main__":
    main()
