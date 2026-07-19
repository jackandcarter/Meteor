# Client Research and Gameplay Restoration

This document is for AetherXIV developers researching a legally obtained Final
Fantasy XIV 1.23b client and implementing verified behavior in the server. It is
not an end-user setup guide.

## Boundaries

- Work only with client files and captures you are authorized to inspect.
- Do not commit or redistribute client executables, assets, patch archives, or
  proprietary extracted data.
- Prefer hashes, offsets, small evidence fixtures, decoded metadata, and written
  provenance over copied client content.
- Do not bypass authentication, access another person's traffic, or collect
  credentials.
- Treat every observation as bounded evidence. One capture does not prove a
  global spawn population, drop rate, patrol, timer, or condition.

## Supported research tools

The repository directly supports:

- [Wireshark and tshark](https://www.wireshark.org/download.html) for packet
  capture filtering, stream selection, and scripted extraction;
- the Python tools under `tools/Universal`;
- `AetherXIV.ClientData` for bounded client-file classification, hashing,
  header probes, and string/resource evidence;
- Core structured JSONL diagnostics;
- Umbra's loopback-only read-only developer bridge;
- protocol, data, and gameplay tests under `tests`.

[Ghidra](https://github.com/NationalSecurityAgency/ghidra/releases) is an
optional static-analysis tool when packet and data evidence cannot explain how
the 1.23b client constructs or consumes a value. Record the executable hash,
image base, function location, and reasoning for any conclusion derived from
disassembly. Ghidra is not required for the standard trace workflow.

## Evidence hierarchy

Use the strongest available source and preserve its provenance:

1. exact client identity and hash;
2. official server/client packet capture with stream and frame numbers;
3. repeatable client-file evidence with file hash and bounded parser output;
4. verified runtime observation through the read-only developer bridge;
5. legacy Meteor/Aether code or SQL as a compatibility reference;
6. in-game `!pinspawn` observations as provisional location notes.

Legacy code proves what the older emulator attempted, not necessarily what the
official server did. `!pinspawn` proves where a developer stood and what they
typed, not the retail actor identity or population rules.

## Packet-trace workflow

### 1. Preserve capture identity

Record the capture SHA-256, collection date, client executable hash, service,
server address, and the scenario performed. Keep the original capture outside
the repository when it contains unrelated traffic or private information.

### 2. Identify a TCP stream

Use Wireshark's conversation and stream views to locate the Lobby, World, or Map
connection. Record the `tcp.stream` index and server port.

### 3. Reassemble and decode

```bash
python3 tools/Universal/analyze-legacy-tcp-stream.py \
  capture.pcapng \
  --stream 12 \
  --server-port 1989 \
  --out evidence/zone162-stream12.json
```

This invokes `tshark` to reassemble both directions and emits structured JSON.
Review framing boundaries, directions, sizes, opcodes, source IDs, and target
IDs before interpreting payload fields.

### 4. Extract a bounded fixture

```bash
python3 tools/Universal/extract-trace-fixtures.py \
  capture.pcapng \
  --service Map \
  --server-host 203.0.113.10 \
  --server-port 1989 \
  --frame-index 1204 \
  --frame-index 1211 \
  --out tests/fixtures/protocol/map-example.json
```

Use exact frame selection whenever possible. A small fixture is reviewable and
does not leak an entire capture into source control.

### 5. Inspect actor properties

```bash
python3 tools/Universal/inspect-actor-properties.py \
  evidence/zone162-stream12.json \
  --direction server-to-client \
  --out evidence/zone162-actor-properties.json
```

Property-name candidates must be independently supported. A readable string
near a value is not automatically its semantic definition.

### 6. Map encounter observations

```bash
python3 tools/Universal/map-trace-encounters.py \
  evidence/zone162-stream12.json \
  --zone-id 162 \
  --zone-evidence "official Map capture, frames 1204-1388" \
  --out evidence/zone162-encounters.json
```

This tool intentionally produces evidence, not SQL. Review it before any data
is promoted into the database.

## Client-file workflow

The `AetherXIV.ClientData` library scans candidate `.gmd`, `.geb`, and sqpack
files without treating every candidate as understood. Its output can include:

- relative path and cryptographic identity;
- file classification;
- bounded header and magic probes;
- string/resource observations;
- candidate offsets and structural patterns;
- warnings and an actor-import focus.

The current repository contains the library and tests, but the historical
client-data-miner CLI entry referenced by an older build script is not present.
Do not document or automate that missing entry until a supported CLI is restored.
Developers can extend the library through tests and a reviewed command-line host.

When implementing a parser:

1. hash the exact input file;
2. begin with bounds-checked structural observations;
3. compare multiple files before naming a field;
4. preserve unknown values rather than assigning guessed meanings;
5. add malformed/truncated input tests;
6. emit evidence metadata separate from canonical server data.

## Umbra read-only developer bridge

Umbra provides a loopback-only development bridge for read-only memory peek and
scan operations. Use it when a runtime value cannot be obtained from packets or
static files.

- Keep the bridge bound to loopback.
- Use the narrowest address range and shortest read possible.
- Record the client hash, module, address/offset, value type, and triggering
  action.
- Repeat the observation across launches before treating an address as stable.
- Do not add arbitrary write, remote-control, or non-loopback capabilities.

The bridge wire format, capabilities, configuration, and examples live in the
[Umbra SDK](../UMBRA_SDK.md).

## Implementing verified behavior

Choose the layer that owns the behavior:

- packet shape or opcode: `AetherXIV.Protocol` and focused protocol tests;
- transport/framing/session behavior: server hosting or the owning service;
- persistent account, character, actor, or content state: repository contracts,
  MariaDB implementation, and a migration;
- Map actor interaction or gameplay rule: direct-core Map logic and/or Lua;
- canonical static data: reviewed seed source and generated database package;
- Launcher presentation/account behavior: Launcher Services and its repository.

For every implementation:

1. cite the evidence in code, fixture metadata, or migration comments;
2. add the smallest failing test first;
3. implement bounds and invalid-state handling;
4. verify the complete client-visible sequence, not only one packet;
5. run `./tools/Development/verify-aetherxiv.sh`;
6. test from a clean database or a reviewed migration path.

## Restoring enemies with `!pinspawn`

`!pinspawn` records the current player's zone, position, and rotation in
`server_battlenpc_spawn_audit_pins`. It is a development evidence tool. It does
not spawn an enemy and does not make the observation canonical.

The command currently has no dedicated access-control check in its built-in
path. Use it only on an isolated development server until access control is
added and verified.

### Interactive mode

At the proposed location, enter:

```text
!pinspawn
```

Then answer the enemy-name and source-note prompts. Use `skip` for a blank source
or `cancel` to stop.

### One-line mode

Quote names or notes containing spaces:

```text
!pinspawn "Diremite" "Observed in developer capture zone 162 frame 1288"
```

The saved row contains the player, character name, zone, X/Y/Z, rotation,
enemy-name note, source note, creation time, and promotion-audit fields.

### Review provisional pins

```sql
SELECT pinId, zoneId, enemyName, positionX, positionY, positionZ, rotation,
       sourceNote, createdAt
FROM server_battlenpc_spawn_audit_pins
WHERE isPromoted = 0
ORDER BY zoneId, enemyName, createdAt;
```

Correlate each pin with independent client, capture, or legacy evidence. Resolve
actor class, presentation, pool, group, level range, HP/MP, AI/combat profile,
respawn behavior, and content ownership separately. Do not infer these fields
from coordinates alone.

### Promote through a migration

A reviewed enemy restoration commonly touches:

- `gamedata_actor_class` for verified class path and presentation properties;
- `server_battlenpc_pools` for actor/combat identity;
- `server_battlenpc_groups` for level, stats, respawn, and zone ownership;
- `server_battlenpc_spawn_locations` for stable coordinates and rotation;
- scripts or director logic when the enemy belongs to an event or guildleve.

Make the migration idempotent, document its evidence, and use stable IDs that do
not collide with existing content. After successful validation, update the
corresponding audit rows with `isPromoted`, `promotedAt`, `promotionMigration`,
and `promotionNote` when those pins were actually used.

`db/direct-core/migrations/20260717_000006_central_shroud_enemy_restore.sql` is
a useful structural example: it restores 23 officially observed zone-162
enemies through actor, pool, group, and spawn data. It was derived from official
capture evidence rather than `!pinspawn` rows, so it must not be used to claim
that unrelated provisional pins were promoted.

## Completion checklist

- [ ] Exact source identity and provenance recorded
- [ ] No guessed semantics presented as confirmed
- [ ] Bounded fixture or parser test added
- [ ] Correct server/data/script layer selected
- [ ] Migration is idempotent and checksum-safe
- [ ] Player data and existing IDs are preserved
- [ ] Invalid and truncated inputs tested
- [ ] Full development verification passes
- [ ] Client-visible behavior tested end to end
- [ ] No client assets or private captures committed
