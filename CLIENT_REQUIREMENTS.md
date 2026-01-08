# Client Requirements Matrix (FFXIV 1.23b)

This document is a living checklist that maps **client behavior** to **server responsibilities**.
Use it to validate each protocol phase (Lobby → World → Map), and to track missing or partial
implementations.

## How to use this matrix

1. **Capture traffic** for the phase you are testing.
2. **Record client→server packets** (opcode, packet type, key fields).
3. **Record expected server responses** (opcode, packet type, key fields).
4. **Mark the status** as Implemented / Partial / Missing.
5. **Link to implementation locations** so changes stay grounded in code.

> Tip: Keep this file open while you instrument unknown opcode logging; every new opcode
> should be added here with a best-effort description and status.

---

## Phase: Lobby (Login, Session, Character List)

| Step | Client → Server | Server → Client | Notes | Status | Code pointers |
| --- | --- | --- | --- | --- | --- |
| Lobby secure handshake | Base packet with ticket/phrase (packet size 0x288, test ticket) | Secure connection acknowledgment | Lobby packet processor handles blowfish key and secure ack. | Implemented | `Lobby Server/PacketProcessor.cs` (`ProcessStartSession`) |
| Session acknowledgment | Subpacket type 3, opcode 0x05 | Account list | Returns hard-coded account list and session validation. | Implemented | `Lobby Server/PacketProcessor.cs` (`ProcessSessionAcknowledgement`) |
| Character list request | Subpacket type 3, opcode 0x03 | World/import/retainer/character lists | Sequence includes world list + import + retainers + characters. | Implemented | `Lobby Server/PacketProcessor.cs` (`ProcessGetCharacters`) |
| Character select | Subpacket type 3, opcode 0x04 | Select character confirm (world address/port) | Builds response from DB world record. | Implemented | `Lobby Server/PacketProcessor.cs` (`ProcessSelectCharacter`) |
| Character modify | Subpacket type 3, opcode 0x0B | Character create/modify responses | Reservation + creation logic; world lookup required. | Partial | `Lobby Server/PacketProcessor.cs` (`ProcessModifyCharacter`) |
| Retainer modify | Subpacket type 3, opcode 0x0F | (Unknown) | Opcode reserved; not implemented. | Missing | `Lobby Server/PacketProcessor.cs` (switch default) |

### Known Lobby gaps
- World population is hard-coded (no dynamic population). Add tracking when session counts are known.

---

## Phase: World (Session Routing, Zone Change)

| Step | Client → Server | Server → Client | Notes | Status | Code pointers |
| --- | --- | --- | --- | --- | --- |
| Initial session open | Subpacket type 0x01 (Hello) | 0x7 + 0x2 responses | Session creation and routing to zone server. | Implemented | `World Server/PacketProcessor.cs` (`ProcessPacket`) |
| Ping | Subpacket type 0x07 | 0x8 ping response | Keeps client alive. | Implemented | `World Server/PacketProcessor.cs` (`ProcessPacket`) |
| Zone change request | Subpacket type >= 0x1000 (0x1002) | Session end + new session start | Routed through world manager to zone servers. | Implemented | `World Server/PacketProcessor.cs` (`ProcessPacket`) |
| Party chat relay | Subpacket opcode 0x00C9 | Party message to members | Relay via world manager. | Implemented | `World Server/PacketProcessor.cs` (`InterceptProcess`) |
| Login trigger | Subpacket opcode 0x0006 | World login flow | Calls world manager login. | Implemented | `World Server/PacketProcessor.cs` (`InterceptProcess`) |

### Known World gaps
- Session collision handling is a TODO (should disconnect prior session cleanly).

---

## Phase: Map (Zone, Actor, Event, Chat)

| Step | Client → Server | Server → Client | Notes | Status | Code pointers |
| --- | --- | --- | --- | --- | --- |
| Session begin | Opcode 0x1000 | Session begin confirm | Adds session and handles zone-in state. | Implemented | `Map Server/PacketProcessor.cs` (`ProcessPacket`) |
| Session end | Opcode 0x1001 | Session end confirm | Cleanup and save, then confirm. | Implemented | `Map Server/PacketProcessor.cs` (`ProcessPacket`) |
| Chat message | Opcode 0x0003 | Broadcast to area | Supports say/shout; GM commands via `!`. | Implemented | `Map Server/PacketProcessor.cs` (`ProcessPacket`) |
| Language code | Opcode 0x0006 | Zone-in/login sequence | Calls `onBeginLogin` and `onLogin`. | Implemented | `Map Server/PacketProcessor.cs` (`ProcessPacket`) |
| Position update | Opcode 0x00CA | Instance update broadcast | Updates actor position and clears zone-change flag. | Implemented | `Map Server/PacketProcessor.cs` (`ProcessPacket`) |
| Event start | Opcode 0x012D | Event routing to actor | Uses static actors, retainers, directors. | Implemented | `Map Server/PacketProcessor.cs` (`ProcessPacket`) |
| Event update | Opcode 0x012E | Event update handling | For event progression in Lua. | Partial | `Map Server/PacketProcessor.cs` (`ProcessPacket`) |

### Known Map gaps
- Bazaar buy/sell operations are TODOs in world manager.
- Trade flow is missing full inventory checks.

---

## Phase: Gameplay systems (Lua, NPCs, Quests)

| System | Client expectation | Current state | Status | Code pointers |
| --- | --- | --- | --- | --- |
| Chocobo lender | Player level check | Level is hard-coded. | Partial | `Data/scripts/base/chara/npc/populace/PopulaceChocoboLender.lua` |
| GC company shop | Rank/city/item-range validation | TODOs in shop logic. | Partial | `Data/scripts/base/chara/npc/populace/PopulaceCompanyShop.lua` |
| Guild shop | Point reset handling | TODO. | Partial | `Data/scripts/base/chara/npc/populace/shop/PopulaceGuildShop.lua` |
| Company supply | Seal cap & availability checks | TODO. | Partial | `Data/scripts/base/chara/npc/populace/PopulaceCompanySupply.lua` |
| Company officer | Rank upgrade and cap handling | TODO. | Partial | `Data/scripts/base/chara/npc/populace/PopulaceCompanyOfficer.lua` |
| Black marketeer | Seal cap + NPC validation | TODOs. | Partial | `Data/scripts/base/chara/npc/populace/PopulaceBlackMarketeer.lua` |
| Company buffer | Sanction buff | TODO. | Partial | `Data/scripts/base/chara/npc/populace/PopulaceCompanyBuffer.lua` |
| Quest tutorials | Timing/workflow issues | Multiple TODO workarounds in directors. | Partial | `Data/scripts/directors/Quest/QuestDirectorMan0g001.lua`, `QuestDirectorMan0l001.lua`, `QuestDirectorMan0u001.lua` |
| Quest rewards | Reward handling | TODO for some quests. | Partial | `Data/scripts/quests/etc/etc3g0.lua` |
| Bazaar trade | Refactor + backend ops | TODO + backend unimplemented. | Partial | `Data/scripts/commands/BazaarTradeCommand.lua` |

---

## Unknown opcode log checklist (to be filled as we instrument)

| Opcode | Packet type | Phase | Client behavior | Expected server response | Status | Notes |
| --- | --- | --- | --- | --- | --- | --- |
| TBD | TBD | TBD | TBD | TBD | Missing | Add entries as soon as they appear in logs. |

---

## Next steps (maintainer checklist)

- [ ] Add unknown opcode logging to Lobby/World/Map packet processors.
- [ ] Capture client traffic for each phase and populate missing rows.
- [ ] Convert “Partial” rows to “Implemented” with concrete validations.
- [ ] Track opcodes that map to Lua scripts (events, quests, NPCs).

