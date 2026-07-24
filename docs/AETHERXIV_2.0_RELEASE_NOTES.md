AetherXIV 2.0 — Core, Launcher & Umbra Release Notes

A new foundation for AetherXIV

AetherXIV 2.0 is more than a version bump. It moves the project onto a modern, cross-platform foundation while preserving the parts of the 1.3 Aether gameplay core that still matter.

This release brings the Lobby, World and Map services into the main 2.0 workspace, replaces the old launcher-service stack, adds a unified server management app, greatly expands Umbra, and restores several pieces of gameplay that were incomplete or unreliable in the previous release.

The goal of 2.0 is simple: make AetherXIV easier to install, launch, operate, test and continue building—without losing the behavior already recovered from FFXIV 1.x.

**Highlights**

- **Build 21988:** restores the historical post-Miounne linkpearl tutorial without the nested Lua handoff that could leave the client event-locked after the Carline Canopy scene. Miounne's first pearl message now leads cleanly to Camp Bentbranch, while interrupted Build 21987 characters retain their pending story state.
- **Build 21987:** repairs the Gridania Carline Canopy login handoff so an interrupted character reconstructs the missing post-cutscene director automatically, and ships the unified progression, character-data, launcher and database fixes from this 2.0 source snapshot.
- **Build 21986:** restores the Gridania opening tutorial and the early `Souls Gone Wild` story route through the Growery, child-emote scenes, White Wolf Gate escort, Lifemend Stump battle and public-Gridania return, including interruption and stale-instance recovery.
- Corrected ZiPatch `0x44` handling so hash-selection records apply their payload instead of deleting valid client files.
- Client memory patches now resolve from the suspended process's actual loaded image base, including the Windows/WOW64 helper path.
- Gridania's opening tutorial now registers retail-shaped content and temporary-party groups atomically, with correct NPC names and a single active content director during zone-in.
- Actor removal, quest-event presentation, private-content teardown and zone finalization now preserve the packet order and object lifetime expected by the client.
- Umbra repository discovery and installation now support server-provided registries, safer downloads, update state and clearer in-game management.
- Database startup now recognizes trusted earlier 2.0 baselines, verifies every migration checksum and canonical seed/schema contract, and falls back to an explicit backed-up clean-install prompt when in-place convergence is unsafe or fails.
- Modernized .NET 10 core and tooling.
- One release layout for the AetherXIV Core app, Launcher, database tools and Umbra.
- New AetherXIV Core app for starting, stopping and monitoring the full server stack.
- Safer database setup, updates, backups and player-data migration.
- Restored opening-instance recovery, attribute-point allocation and guildleve functionality.
- Restored 23 ambient enemies in the Central Shroud.
- Redesigned launcher home screen and server-managed news/reel presentation.
- Umbra API 2.0 with an in-game plugin library, managed plugin host and customizable UI.
- Build and packaging tools for macOS, Linux, SteamOS and Windows.
- 560 automated Core, protocol, data, launcher, server-management and Umbra checks passing for this release snapshot.

---

## Core updates

- The original Lobby, World and Map services now live inside the 2.0 solution and build as part of the same release.
- Core behavior was carried forward into the unified AetherXIV 2.0 source tree.
- Server paths now resolve from the installed application instead of depending on the folder from which a service was started. This fixes missing config, script and navmesh files in packaged builds.
- Lobby, World and Map can receive their database host, port, database name and credentials from the new AetherXIV Core app.
- Runtime files and Lua scripts have integrity manifests so incomplete or mixed releases can be detected before launch.
- The server transport and protocol foundation now has typed, byte-checked coverage for login, actors, inventory, battle, chat, social groups, events, player state, countdowns and zone transitions.
- Official packet captures are represented by focused test fixtures. This lets future fixes be checked against observed client behavior instead of guesswork.


__                                                                         __
**Gameplay restored or implemented**
__                                                                         __

- **Opening instance recovery:** Limsa Lominsa, Gridania and Ul'dah opening content can now be reconstructed after a server restart or interrupted session. Players are returned to the correct private battle/content area instead of being dropped into a conflicting public scene.
- **Post-battle recovery:** Characters whose opening quest has already reached its post-battle phase are moved into the correct follow-up area and checkpoint.
- **Attribute points:** The Bonus Point window now shows the character's real available points, validates the submitted allocation, saves it per class and recalculates the affected stats. The old hard-coded placeholder values are gone.
- **Regional guildleve acceptance:** Gontrant's guildleve menu in Gridania now offers its restored cards, prevents duplicate acceptance, checks journal space and saves the selected leve before updating the client.
- **Local guildleve acceptance:** Tierney's local leve flow is implemented with separate local journal slots, duplicate protection and persistence.
- **Guildleve content:** The observed Central Shroud guildleve scene now creates its director, party content group, bonus object, search points, objective state and map markers. Eligible party members in the same zone are included.
- **Central Shroud population:** 23 observed ambient enemies have been restored across nine enemy families, including bats, ladybugs, wolves, flowers, nuteaters, glirulus, crabs and monkeys.
- **Player-specific event states:** Scripted event-condition changes are now actually sent to the client, restoring logic used to enable or disable interactions for an individual player.
- **Gridania opening story:** The opening battle, post-battle transition, Carline Canopy handoff, guild visits, Growery emote sequence, White Wolf Gate escort, staged enemy encounters, Lifemend Stump scenes and return to public Gridania now have a continuous server-side quest contract.
- **Interrupted-duty recovery:** Logging out, restarting the stack or leaving transient quest content no longer leaves the character permanently bound to a stale private area. The active quest phase reconstructs the appropriate public or private presentation on return.


__                                                                         __
**Gameplay and stability fixes**
__                                                                         __

- Dynamic actors and directors now share a safe, non-recycled actor-number sequence. This prevents actor ID collisions after despawns or while entering private content.
- Guildleve directors now use the correct actor family and group type, include themselves in their content group and avoid duplicate group members.
- Local and regional guildleves now use their correct journal ranges instead of overwriting one another.
- Guildleve saves are transactional: a failed database write no longer leaves the client and server disagreeing about the accepted leve.
- Invalid or out-of-range saved guildleve rows are ignored safely instead of corrupting the player's journal state.
- Initial equipment packets now target the intended player correctly.
- Logout and Quit Game begin cleanup immediately, and repeated World/Map close messages can no longer save or remove the same player twice.
- A stray enemy hate-state override was removed so the existing passive, engaged and party-aware logic is no longer discarded.
- Equipment changes now produce focused diagnostics without applying an unverified blanket stat bonus. Weapon type, delay and hit-count behavior remains intact while full equipment scaling is researched.
- Navigation data is loaded from the packaged server location, fixing pathfinding startup failures.

__                                                                         __
**Database changes**
__                                                                         __

- 2.0 ships a canonical direct-core database baseline instead of relying on a collection of manual imports.
- A database compatibility record now prevents the Core from starting against the wrong schema generation or an incomplete database.
- Applied migrations are recorded with checksums. Modified or mismatched migration files are rejected instead of being silently reapplied.
- Database setup verifies required tables and seed data before reporting success.
- A missing database and application account are created automatically after the operator supplies one-time administrator credentials. A manually created empty schema is also handled correctly.
- Existing AetherXIV 2 installations are backed up, checked against their migration ledger, updated in place, and verified for required tables, columns, and seeds. Older or damaged installations receive a complete verified backup followed by a clean canonical installation.
- When compatible player tables are present, canonical repair attempts to preserve accounts, characters, and `characters_`-owned data. Account and character totals must match.
- If player data cannot be transferred safely, the clean canonical database is retained along with the untouched full backup and player-data export for manual recovery. The old database is restored automatically only if the canonical database itself cannot be installed or rebuilt.
- Development-only actor workbench and decode-staging tables are removed from the runtime database.
- New tables support launcher presentation, news, reel captions, patch catalogs, Umbra framework artifacts, plugin repositories and plugin blocks.
- Class attribute allocations now have a canonical persistence table.
- Guildleve search-point actor data and Central Shroud enemy pools, groups and spawn positions are included as production migrations.

**Seed summary**

- 111 zones.
- 835 actor classes.
- 826 actor appearances.
- 993 static actor spawns.
- 734 quest actor records.
- 1,201 tracked Lua scripts.
- Invalid and orphaned actor relationships are excluded rather than filled with invented defaults.

__                                                                         __
**AetherXIV Core app & server operation**
__                                                                         __

2.0 adds the windowed AetherXIV Core app for running and maintaining a local or hosted stack. It launches the server services directly in the background and shows their output inside the app—no terminal window is opened.


- Start or stop Lobby, World, Map and Launcher Services together or control them individually.
- See live service state, process IDs, configured endpoints and per-service logs in one place.
- Configure bind/advertised addresses, database settings, data paths and diagnostic levels from one app.
- Public World/Lobby configuration catches loopback-only listeners before startup, with a one-click option to use public listener binds while preserving the configured ports.
- Run dependency and database preflight checks before services start.
- Create or repair the database through a guided admin-credential prompt; admin credentials are not saved.
- Receive a clear canonical-repair prompt when the installed database is older than AetherXIV 2 or incomplete.
- Create, schedule, edit and remove launcher news posts.
- Customize news title, summary and body colors.
- Add optional headers, subtext, sizes and colors to individual launcher reel images.
- Live log buffering is bounded so a long-running server cannot grow the UI log without limit.

__                                                                         __
**Launcher updates**
__                                                                         __

- Echo Gate has become **AetherXIV Launcher** and is packaged as part of the 2.0 release.
- The home screen has been redesigned with new AetherXIV branding, a darker visual theme, animated ambient particles and an automatic image reel with manual navigation.
- Reel captions and news presentation can be changed from the AetherXIV Core app without rebuilding the launcher.
- Launcher news supports per-section colors, scheduled publication, banners and links.
- The old PHP launcher-service dependency has been replaced by a modern launcher host for config, status, news, login, account creation, patches and Umbra catalogs.
- Localhost, project-server and custom server profiles are easier to switch between and are saved between sessions.
- Client validation still checks the selected FFXIV 1.x install before enabling launch.
- Patch downloads reuse files that are already valid, and the complete patch chain is checksum-verified before application.
- Patch and managed Wine downloads show verified progress.
- The launcher can open the FFXIV configuration tool against the correct native or Wine profile.
- macOS, Linux and SteamOS can install a platform-selected, checksum-pinned managed Wine 11.0 runtime directly from the Launcher, without using the game server as a catalog. On Apple silicon, validation waits for Apple's Rosetta installation prompt; on Linux it reports missing host libraries before Wine starts. Local Wine installations remain detectable, and runtime/prefix/helper validation is required before launch. Windows launches the client directly.
- Runtime validation, prefix preparation, client-helper selection, Umbra injection and launch logs are collected into one launch plan with clearer failure messages.
- Framework, plugin repository and blocklist locations are supplied by the selected server instead of being fixed in the launcher.

__                                                                         __
**Umbra API 2.0**
__                                                                         __

Umbra has grown from bootstrap/catalog groundwork into an in-game framework with a native DirectX 9 overlay and a managed plugin runtime.

- New Umbra dock and in-game plugin library.
- Discover, Installed, Updates, Repositories, Settings and About views.
- Install, enable, disable, reload and uninstall supported plugins from the in-game manager.
- Searchable plugin lists with status, version, author, permissions and performance information.
- Safe mode can block third-party plugin activation while leaving the framework available.
- Multiple server-provided repositories, compatibility rules and plugin blocklists are supported.
- Customizable appearance profiles with opacity, font size, interface scale, corner rounding, gradients and editable colors.
- Built-in live log, framework readiness and DirectX 9/render diagnostics.

__                                                                         __
**Plugin framework**
__                                                                         __

- Umbra Plugin API 2.0 provides lifecycle, update and in-game draw callbacks.
- Plugins load in separate unloadable contexts and receive their own configuration directory and logger.
- Manifest-declared capabilities control access to command, chat and appearance services.
- The draw API includes windows, panels, text styles, buttons, toggles, inputs, badges, icons, artwork and color controls.
- Plugin callback failures are contained and faulting plugins are quarantined instead of taking down the complete managed host.
- Draw time is measured against a small frame budget and slow callbacks are surfaced in diagnostics.
- A sample plugin and a deliberately faulting test plugin are included for framework validation.

**Umbra limitations in this release:** the plugin command framework is available, but native FFXIV chat submission/printing and live actor-appearance reading remain disabled until a later update. Umbra reports these services as unavailable rather than guessing at client memory.


__                                                                         __
**Platform and packaging updates**
__                                                                         __

- **macOS:** dedicated Apple Silicon build path, native Launcher and AetherXIV Core app bundles, plus packaged Windows helpers and Umbra payloads for Wine.
- **Linux:** dedicated x64 build path with windowed Launcher and AetherXIV Core apps, the server services, Windows client helpers and Umbra payload.
- **SteamOS / Steam Deck:** a separate build entry based on the Linux runtime, ready for continued device validation.
- Linux and SteamOS releases are relocatable and launch through the native Core and Launcher executables in their `app` folders; unreliable path-dependent desktop shortcuts are not packaged.
- **Windows:** dedicated x64 Core and Launcher app packaging with native x86 Umbra injector and bootstrap builds for the 32-bit game client.
- Every platform build creates the same organized release layout for servers, launcher, UI, database and runtime assets.
- Release verification rejects source files in the output, missing executables and incomplete runtime data.
- Development verification can build both solutions and run all tests from one entry point.
- Existing platform build commands now report missing SDK, Python and native compiler prerequisites before modifying release output. Docker release validation also starts a temporary MariaDB-backed Compose stack and checks service health.

__                                                                         __
**Debug and diagnostics**
__                                                                         __

- Structured diagnostic files can be enabled for Lobby, World and Map without changing normal player-facing behavior.
- Traces cover service lifecycle, connections, packet classifications, login/handoff, zone entry, private content recovery, guildleve acceptance, stat allocation, equipment changes and logout cleanup.
- Diagnostic artifacts use stable event names and can be compared with the included official trace fixtures.
- Unknown or provisional packet behavior remains visible to developers without flooding normal release logs.
- Umbra adds native bootstrap, injection, DirectX 9 hook, managed-host, frame timing and plugin-state diagnostics.
- A read-only development bridge and live in-game developer menu are available when developer options are explicitly enabled.

__                                                                         __
**What is still in progress**
__                                                                         __

AetherXIV 2.0 is a major foundation and restoration release, not a claim that the full original game is complete.

- Gridania's restored opening and early story route has been exercised through its major tutorial, guild, escort, battle and return checkpoints. Later Gridania story content and the equivalent Limsa Lominsa and Ul'dah routes still need the same trace-backed restoration depth.
- The restored Central Shroud guildleve has its observed director, objects, markers and party state, but its enemy waves and complete combat/objective behavior are still being reconstructed.
- Most guildleves and quests do not yet have complete retail-faithful scripts.
- Full equipment stat scaling is intentionally deferred until the conditional item data is understood; unverified bonuses are not applied.
- Battle AI, skills, status effects, drops, enmity and party combat require broader content-by-content validation.
- Trade, bazaar, crafting, gathering, retainers, linkshells and other long-tail systems remain partial or need deeper persistence testing.
- Native Umbra chat and live actor-appearance adapters are not enabled yet.
- Hosted patch/runtime/framework catalogs still depend on what each server administrator publishes.
- Steam Deck hardware behavior and ARM client-launch paths still need wider testing. The current primary packaged targets are macOS Apple Silicon, Linux x64 and Windows x64.

Please report reproducible issues with the character, zone, action taken and any launcher/server log attached. That information directly helps us turn the remaining partial systems into verified gameplay.
