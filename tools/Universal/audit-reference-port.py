#!/usr/bin/env python3
"""Build a conservative source-level inventory for the legacy port audit."""

from __future__ import annotations

import argparse
import hashlib
import json
import re
import subprocess
from collections import Counter
from pathlib import Path


SYMBOL = re.compile(r"\b(?:class|struct|interface|enum)\s+([A-Za-z_][A-Za-z0-9_]*)")


def family(relative: Path) -> str:
    text = relative.as_posix()
    if "/Packets/" in f"/{text}":
        if "/WorldPackets/" in f"/{text}":
            return "server-route-packet"
        return "client-packet"
    if "/Actors/" in f"/{text}" or "/Actor/" in f"/{text}":
        return "actor-runtime"
    if "/Lua/" in f"/{text}":
        return "scripting-runtime"
    if "/DataObjects/" in f"/{text}" or relative.name == "Database.cs":
        return "persistence-state"
    if relative.name in {"PacketProcessor.cs", "Server.cs", "ClientConnection.cs"}:
        return "transport-dispatch"
    if relative.name.endswith("Manager.cs") or relative.name in {"WorldManager.cs", "WorldMaster.cs"}:
        return "service-runtime"
    return "service-support"


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--reference-root", required=True, type=Path)
    parser.add_argument("--native-root", required=True, type=Path)
    parser.add_argument("--json", required=True, type=Path)
    parser.add_argument("--markdown", required=True, type=Path)
    parser.add_argument("--overrides", type=Path)
    args = parser.parse_args()

    components = ("Common Class Lib", "Lobby Server", "World Server", "Map Server")
    reference_files = sorted(
        path for component in components
        for path in (args.reference_root / component).rglob("*.cs")
        if "/obj/" not in path.as_posix() and "/bin/" not in path.as_posix()
    )
    native_text = "\n".join(
        path.read_text(errors="replace")
        for path in sorted((args.native_root / "src").rglob("*.cs"))
    )
    reference_text = "\n".join(path.read_text(errors="replace") for path in reference_files)

    overrides = {"componentDefaults": {}, "entries": {}}
    if args.overrides and args.overrides.exists():
        overrides = json.loads(args.overrides.read_text())

    entries = []
    for path in reference_files:
        text = path.read_text(errors="replace")
        relative = path.relative_to(args.reference_root)
        symbols = sorted(set(SYMBOL.findall(text)))
        native_symbols = [symbol for symbol in symbols if re.search(rf"\b{re.escape(symbol)}\b", native_text)]
        referenced_symbols = [
            symbol for symbol in symbols
            if len(re.findall(rf"\b{re.escape(symbol)}\b", reference_text)) > len(re.findall(rf"\b{re.escape(symbol)}\b", text))
        ]
        entry = {
            "referencePath": relative.as_posix(),
            "component": relative.parts[0],
            "family": family(relative),
            "sha256": hashlib.sha256(path.read_bytes()).hexdigest(),
            "lines": len(text.splitlines()),
            "symbols": symbols,
            "referencedSymbols": referenced_symbols,
            "nativeSymbolCandidates": native_symbols,
            "disposition": "native-candidate-unverified" if native_symbols else "unmapped-needs-review",
            "nativeOwners": [],
            "evidence": [],
            "tests": [],
            "notes": ""
        }
        reviewed = dict(overrides.get("componentDefaults", {}).get(entry["component"], {}))
        reviewed.update(overrides.get("entries", {}).get(entry["referencePath"], {}))
        entry.update(reviewed)
        entries.append(entry)

    reference_commit = subprocess.run(
        ["git", "-C", str(args.reference_root), "rev-parse", "HEAD"],
        check=True,
        capture_output=True,
        text=True,
    ).stdout.strip()
    tree_hasher = hashlib.sha256()
    for entry in entries:
        tree_hasher.update(entry["referencePath"].encode("utf-8"))
        tree_hasher.update(b"\0")
        tree_hasher.update(bytes.fromhex(entry["sha256"]))

    payload = {
        "schema": "aetherxiv.reference-port-inventory.v1",
        "policy": "A symbol match is only a review candidate; it does not establish behavioral parity.",
        "authority": "The immutable legacy file hashes are source authority; this generated inventory is only a tracking index.",
        "referenceCommit": reference_commit,
        "referenceTreeSha256": tree_hasher.hexdigest(),
        "referenceRoot": str(args.reference_root),
        "nativeRoot": str(args.native_root),
        "entries": entries,
    }
    args.json.parent.mkdir(parents=True, exist_ok=True)
    args.json.write_text(json.dumps(payload, indent=2) + "\n")

    dispositions = Counter(entry["disposition"] for entry in entries)
    families = Counter(entry["family"] for entry in entries)
    lines = [
        "# Generated Legacy Source Port Inventory",
        "",
        "This report is conservative. A native symbol match is not completion; each reachable",
        "behavior still requires an owner, evidence, and a test before its disposition can be",
        "promoted to `verified-native`.",
        "",
        f"- Reference commit: `{reference_commit}`",
        f"- Reference C# tree SHA-256: `{tree_hasher.hexdigest()}`",
        f"- Source files: {len(entries)}",
        f"- Verified native files: {dispositions['verified-native']}",
        f"- Native candidates requiring proof: {dispositions['native-candidate-unverified']}",
        f"- Unmapped files requiring review: {dispositions['unmapped-needs-review']}",
        "",
        "## Families",
        "",
        "| Family | Files |",
        "| --- | ---: |",
    ]
    lines.extend(f"| {name} | {count} |" for name, count in sorted(families.items()))
    lines.extend(["", "## Source Dispositions", "", "| Reference source | Family | Disposition | Native symbol candidates |", "| --- | --- | --- | --- |"])
    for entry in entries:
        candidates = ", ".join(f"`{value}`" for value in entry["nativeSymbolCandidates"]) or "-"
        lines.append(f"| `{entry['referencePath']}` | {entry['family']} | {entry['disposition']} | {candidates} |")
    args.markdown.parent.mkdir(parents=True, exist_ok=True)
    args.markdown.write_text("\n".join(lines) + "\n")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
