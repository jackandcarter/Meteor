from __future__ import annotations

import importlib.util
import struct
import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
SPEC = importlib.util.spec_from_file_location(
    "analyze_legacy_tcp_stream", ROOT / "tools" / "Universal" / "analyze-legacy-tcp-stream.py"
)
MODULE = importlib.util.module_from_spec(SPEC)
assert SPEC.loader is not None
SPEC.loader.exec_module(MODULE)


class AnalyzeLegacyTcpStreamTests(unittest.TestCase):
    def test_correlates_reassembled_frame_to_first_pcap_frame(self) -> None:
        subpacket = struct.pack("<HHIII", 16, 1, 0x10000001, 0x10000001, 0)
        base_header = bytearray(16)
        struct.pack_into("<HH", base_header, 4, 32, 1)
        struct.pack_into("<Q", base_header, 8, 1234)
        stream = bytes(base_header) + subpacket
        segments = [
            {"streamOffset": 0, "payloadLength": 32, "frameNumber": 9, "captureTimestamp": "2.0"},
            {"streamOffset": 0, "payloadLength": 32, "frameNumber": 7, "captureTimestamp": "1.0"},
        ]

        decoded = MODULE.decode_frames(stream, segments)

        self.assertEqual(7, decoded[0]["frameNumber"])
        self.assertEqual("1.0", decoded[0]["captureTimestamp"])
        self.assertEqual("0x0001", decoded[0]["subpackets"][0]["opcode"])


if __name__ == "__main__":
    unittest.main()
