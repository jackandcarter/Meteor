#!/usr/bin/env python3
import argparse
import json
import re
import shutil
import subprocess
import zlib
from pathlib import Path


TCPDUMP_LINE = re.compile(
    r"^(?P<time>\d{4}-\d{2}-\d{2} .*?) IP (?P<src>\S+) > (?P<dst>\S+): .* length (?P<length>\d+)$"
)


def split_endpoint(value):
    host, port = value.rsplit(".", 1)
    return host, int(port.rstrip(":"))


def direction(src_host, src_port, dst_host, dst_port, server_host, server_port):
    if src_port == server_port and (not server_host or src_host == server_host):
        return "server-to-client"
    if dst_port == server_port and (not server_host or dst_host == server_host):
        return "client-to-server"
    return "unknown"


def compression_state(payload_hex):
    if not payload_hex:
        return "unknown"
    normalized = payload_hex.replace(":", "").lower()
    if len(normalized) >= 34 and normalized[2:4] != "00":
        body = normalized[32:]
        return "zlib-body" if body.startswith(("789c", "78da", "7801")) else "compressed-flag-set"
    return "not-compressed"


def base_frame_bodies(payload_hex):
    if not payload_hex:
        return []
    normalized = payload_hex.replace(":", "").lower()
    if len(normalized) < 32:
        return []

    payload = bytes.fromhex(normalized)
    frames = []
    offset = 0
    while offset + 16 <= len(payload):
        packet_size = int.from_bytes(payload[offset + 4 : offset + 6], byteorder="little")
        if packet_size < 16 or offset + packet_size > len(payload):
            break

        frame = payload[offset : offset + packet_size]
        body = frame[16:]
        if frame[1] != 0:
            try:
                body = zlib.decompress(body)
            except zlib.error:
                break

        frames.append(body)
        offset += packet_size

    return frames

def connection_type(payload_hex):
    if not payload_hex:
        return None
    normalized = payload_hex.replace(":", "").lower()
    if len(normalized) < 8:
        return None
    return int.from_bytes(bytes.fromhex(normalized[4:8]), byteorder="little")


def opcode_key(payload_hex):
    keys = message_keys(payload_hex)
    if not keys:
        return None

    return keys[0]["subcode"]


def message_keys(payload_hex):
    keys = []
    for body in base_frame_bodies(payload_hex):
        offset = 0
        while offset + 16 <= len(body):
            message_length = int.from_bytes(body[offset : offset + 2], byteorder="little")
            if message_length < 16 or offset + message_length > len(body):
                break

            message_type = int.from_bytes(body[offset + 2 : offset + 4], byteorder="little")
            category = 0
            subcode = message_type
            if message_type == 0x0003 and message_length >= 32:
                category = int.from_bytes(body[offset + 16 : offset + 18], byteorder="little")
                if category == 0x0014:
                    subcode = int.from_bytes(body[offset + 18 : offset + 20], byteorder="little")

            keys.append(
                {
                    "messageType": f"0x{message_type:04X}",
                    "category": f"0x{category:04X}",
                    "subcode": f"0x{subcode:04X}",
                    "messageLength": message_length,
                }
            )
            offset += message_length

    return keys


def load_with_tshark(capture, args):
    display_filter = f"tcp.port == {args.server_port} && tcp.len > 0"
    if args.frame_index:
        selected_frames = " || ".join(f"frame.number == {index}" for index in args.frame_index)
        display_filter += f" && ({selected_frames})"

    command = [
        "tshark",
        "-r",
        str(capture),
        "-Y",
        display_filter,
        "-T",
        "fields",
        "-e",
        "frame.number",
        "-e",
        "frame.time_epoch",
        "-e",
        "ip.src",
        "-e",
        "tcp.srcport",
        "-e",
        "ip.dst",
        "-e",
        "tcp.dstport",
        "-e",
        "tcp.len",
        "-e",
        "tcp.payload",
        "-E",
        "separator=,",
        "-E",
        "quote=d",
    ]
    completed = subprocess.run(command, check=True, text=True, capture_output=True)
    frames = []
    for row in completed.stdout.splitlines():
        if not row.strip():
            continue
        fields = [field.strip('"') for field in row.split(",")]
        if len(fields) < 8:
            continue
        frame_number, timestamp, src_host, src_port, dst_host, dst_port, tcp_len, payload_hex = fields[:8]
        frames.append(
            {
                "captureName": capture.name,
                "service": args.service,
                "direction": direction(src_host, int(src_port), dst_host, int(dst_port), args.server_host, args.server_port),
                "frameIndex": int(frame_number),
                "timestamp": timestamp,
                "connectionType": connection_type(payload_hex),
                "opcodeKey": opcode_key(payload_hex),
                "messageKeys": message_keys(payload_hex),
                "payloadLength": int(tcp_len or 0),
                "payloadHex": payload_hex.replace(":", "") if payload_hex else None,
                "compressionState": compression_state(payload_hex),
                "evidenceStatus": "TraceConfirmed",
                "source": f"{src_host}:{src_port}",
                "destination": f"{dst_host}:{dst_port}",
            }
        )
        if len(frames) >= args.limit:
            break
    return frames, []


def load_with_tcpdump(capture, args):
    command = ["tcpdump", "-nn", "-tttt", "-r", str(capture), f"tcp and port {args.server_port}"]
    completed = subprocess.run(command, check=True, text=True, capture_output=True)
    frames = []
    for line in completed.stdout.splitlines():
        match = TCPDUMP_LINE.match(line.strip())
        if not match:
            continue
        length = int(match.group("length"))
        if length <= 0:
            continue
        src_host, src_port = split_endpoint(match.group("src"))
        dst_host, dst_port = split_endpoint(match.group("dst"))
        frames.append(
            {
                "captureName": capture.name,
                "service": args.service,
                "direction": direction(src_host, src_port, dst_host, dst_port, args.server_host, args.server_port),
                "frameIndex": len(frames) + 1,
                "timestamp": match.group("time"),
                "connectionType": None,
                "opcodeKey": None,
                "messageKeys": [],
                "payloadLength": length,
                "payloadHex": None,
                "compressionState": "unknown",
                "evidenceStatus": "TraceConfirmed",
                "source": f"{src_host}:{src_port}",
                "destination": f"{dst_host}:{dst_port}",
            }
        )
        if len(frames) >= args.limit:
            break
    return frames, ["tcp.payload unavailable because tshark was not found; install tshark to emit payloadHex and compression candidates."]


def extract_capture(capture, args):
    if shutil.which("tshark"):
        frames, issues = load_with_tshark(capture, args)
        tool = "tshark"
    else:
        frames, issues = load_with_tcpdump(capture, args)
        tool = "tcpdump"
    return {
        "capture": capture.name,
        "tool": tool,
        "frames": frames,
        "accessIssues": issues,
    }


def main():
    parser = argparse.ArgumentParser(description="Extract bounded official FFXIV trace fixtures for protocol evidence.")
    parser.add_argument("captures", nargs="+", help="pcapng capture paths")
    parser.add_argument("--service", default="World", help="service label, for example Lobby, World, or Map")
    parser.add_argument("--server-host", default="", help="official server IP to disambiguate direction")
    parser.add_argument("--server-port", type=int, default=54992, help="service TCP port")
    parser.add_argument("--limit", type=int, default=50, help="maximum frames per capture")
    parser.add_argument(
        "--frame-index",
        type=int,
        action="append",
        default=[],
        help="select an exact capture frame number; may be repeated",
    )
    parser.add_argument("--out", help="output JSON path; stdout when omitted")
    args = parser.parse_args()

    fixture = {
        "schema": "aetherxiv.trace.fixture.v1",
        "service": args.service,
        "serverHost": args.server_host or None,
        "serverPort": args.server_port,
        "captures": [extract_capture(Path(capture), args) for capture in args.captures],
    }

    output = json.dumps(fixture, indent=2)
    if args.out:
        Path(args.out).write_text(output + "\n", encoding="utf-8")
    else:
        print(output)


if __name__ == "__main__":
    main()
