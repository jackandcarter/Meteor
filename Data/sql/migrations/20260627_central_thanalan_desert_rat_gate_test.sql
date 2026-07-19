-- Test-only Central Thanalan gate-side Desert Rat group.
--
-- Purpose:
--   Add one plausible late-1.x gate-side enemy family using the current battle
--   NPC schema and actor data that this repo can safely instantiate today.
--
-- Evidence:
--   - Zone 170 is Central Thanalan.
--   - LemmingStandard actor classes are present and battle-tested in zone 170.
--   - Rodent genus 12 exists and is already used by the current wharf_rat pool.
--   - AI/user-provided restoration notes identify rats as one of the beginner
--     enemies nearest Ul'dah's Central Thanalan gate.
--
-- Caveats:
--   TEST_ONLY / Hypothesis placement. These coordinates use the repo's current
--   local Central Thanalan coordinate space near the known Ul'dah handoff, not
--   final extracted retail spawn data. The loader uses Random.Next(min, max),
--   so maxLevel is upper-exclusive in practice.

INSERT IGNORE INTO `server_battlenpc_pools`
  (`poolId`, `actorClassId`, `name`, `genusId`, `currentJob`, `combatSkill`,
   `combatDelay`, `combatDmgMult`, `aggroType`, `immunity`, `linkType`,
   `spellListId`, `skillListId`)
VALUES
  (101, 2104002, 'desert_rat', 12, 0, 1,
   4200, 1, 0, 0, 0,
   0, 0);

INSERT IGNORE INTO `server_battlenpc_groups`
  (`groupId`, `poolId`, `scriptName`, `minLevel`, `maxLevel`, `respawnTime`,
   `hp`, `mp`, `dropListId`, `allegiance`, `spawnType`, `animationId`,
   `actorState`, `privateAreaName`, `privateAreaLevel`, `zoneId`)
VALUES
  (101, 101, 'desert_rat', 1, 5, 30,
   0, 0, 0, 0, 0, 0,
   0, '', 0, 170);

INSERT IGNORE INTO `server_battlenpc_spawn_locations`
  (`bnpcId`, `customDisplayName`, `groupId`, `positionX`, `positionY`, `positionZ`, `rotation`)
VALUES
  (10001, 'Desert Rat', 101,  42.50, 200.10, -482.00, -2.75),
  (10002, 'Desert Rat', 101,  28.25, 200.00, -486.50,  2.40),
  (10003, 'Desert Rat', 101,  47.00, 200.10, -472.25, -1.95),
  (10004, 'Desert Rat', 101,  31.00, 200.00, -467.50,  1.65);
