from __future__ import annotations

import importlib.util
import struct
import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
SPEC = importlib.util.spec_from_file_location(
    "map_trace_encounters", ROOT / "tools" / "Universal" / "map-trace-encounters.py"
)
MODULE = importlib.util.module_from_spec(SPEC)
assert SPEC.loader is not None
SPEC.loader.exec_module(MODULE)


def packet(opcode: str, source: int, payload: bytes) -> dict:
    return {"opcode": opcode, "sourceActorId": source, "payloadHex": payload.hex()}


class MapTraceEncounterTests(unittest.TestCase):
    def test_decodes_actor_class_path_id_and_correlated_position(self) -> None:
        actor_id = (4 << 28) | (151 << 19) | 7
        player_id = 0x10000001
        actor_init = bytearray(68)
        actor_init[4 : 4 + len(b"mauh_lihzeh")] = b"mauh_lihzeh"
        actor_init[36 : 36 + len(b"PopulaceItemRepairer")] = b"PopulaceItemRepairer"
        actor_init += b"\x02/Chara/Npc/Populace/PopulaceItemRepairer\0"
        actor_init += (b"\x00" + struct.pack(">i", 0)) * 5
        actor_init += b"\x01" + struct.pack(">I", 1500252) + b"\x0f"

        actor_position = bytearray(24)
        struct.pack_into("<ffff", actor_position, 8, 1703.37, 20.57, -850.32, -3.04)
        player_position = bytearray(24)
        struct.pack_into("<ffff", player_position, 8, 1700.0, 20.57, -850.0, 0.0)
        target = struct.pack("<II", actor_id, 0xE0000000)

        document = {
            "schema": "aetherxiv.trace.tcp-stream.v1",
            "capture": "repair_items.pcapng",
            "captureStart": "2012-12-30T22:27:37Z",
            "tcpStream": 0,
            "directions": [
                {
                    "direction": "server-to-client",
                    "frames": [
                        {"timestamp": 1.0, "frameNumber": 12, "captureTimestamp": "1.0", "subpackets": [packet("0x00CC", actor_id, bytes(actor_init))]},
                        {"timestamp": 2.0, "frameNumber": 14, "captureTimestamp": "2.0", "subpackets": [packet("0x00CE", actor_id, bytes(actor_position))]},
                    ],
                },
                {
                    "direction": "client-to-server",
                    "frames": [
                        {"timestamp": 2.5, "subpackets": [packet("0x00CA", player_id, bytes(player_position))]},
                        {"timestamp": 3.0, "subpackets": [packet("0x00CD", player_id, target)]},
                    ],
                },
            ],
        }

        result = MODULE.map_observations(document, 151, "fixture", capture_sha256="a" * 64)
        observation = result["interactions"][0]
        self.assertEqual("aetherxiv.trace.encounter-observations.v2", result["schema"])
        self.assertEqual(1500252, observation["identity"]["actorClassId"])
        self.assertEqual(
            "/Chara/Npc/Populace/PopulaceItemRepairer", observation["identity"]["classPath"]
        )
        self.assertEqual("static-populace-candidate", observation["classification"])
        self.assertAlmostEqual(1703.37, observation["targetPosition"]["x"], places=2)
        self.assertEqual(14, observation["targetPositionSource"]["frameNumber"])
        self.assertEqual("2012-12-30T22:27:37Z", result["source"]["captureStart"])
        self.assertEqual("observation-only", observation["evidenceStatus"])
        self.assertNotIn("productionSql", result)

    def test_decodes_inventory_reference_without_promoting_it(self) -> None:
        encoded = b"\x07" + struct.pack(">I", 0x10000001) + bytes((0, 12, 0)) + b"\x0f"
        parameters = MODULE.decode_lua_parameters(encoded)
        self.assertEqual("item-reference", parameters[0]["kind"])
        self.assertEqual(12, parameters[0]["value"]["slot"])
        self.assertEqual(0, parameters[0]["value"]["itemPackage"])


if __name__ == "__main__":
    unittest.main()
