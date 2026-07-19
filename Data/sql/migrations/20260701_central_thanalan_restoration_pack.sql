-- Central Thanalan enemy restoration pack, first durable milestone.
--
-- Evidence posture:
--   - Runtime loaders use these rows; the evidence table is audit metadata only.
--   - Coordinates are PROVISIONAL. They come from local live trace positions where
--     the actor classes were visually spawned and script-bound in zone 170.
--   - Actor identity uses repo/client data already present in gamedata_actor_class
--     and gamedata_actor_appearance. Blank class-path actors are not promoted here.

CREATE TABLE IF NOT EXISTS `server_battlenpc_restoration_evidence` (
  `evidenceKey` varchar(128) NOT NULL,
  `subjectType` varchar(32) NOT NULL,
  `subjectId` int(10) unsigned NOT NULL DEFAULT '0',
  `zoneId` int(10) unsigned DEFAULT NULL,
  `evidenceStatus` varchar(32) NOT NULL DEFAULT 'provisional',
  `sourceType` varchar(32) NOT NULL DEFAULT '',
  `sourceRef` varchar(255) NOT NULL DEFAULT '',
  `notes` varchar(255) NOT NULL DEFAULT '',
  `createdAt` timestamp NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (`evidenceKey`),
  KEY `idx_battlenpc_restoration_subject` (`subjectType`, `subjectId`),
  KEY `idx_battlenpc_restoration_zone` (`zoneId`, `evidenceStatus`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8;

SET @battle_notice_event = '{"talkEventConditions":[],"noticeEventConditions":[{"unknown1":1,"unknown2":0,"conditionName":"noticeEvent"}],"emoteEventConditions":[],"pushWithCircleEventConditions":[]}';

-- Promote only known nonblank monster class paths that were live-previewed and
-- script-bound in zone 170 traces. This mirrors the working battle-NPC
-- presentation shape without blanket-changing incomplete actor rows.
UPDATE `gamedata_actor_class`
   SET `propertyFlags` = 23,
       `eventConditions` = CASE
           WHEN `eventConditions` IS NULL OR TRIM(`eventConditions`) = '' OR TRIM(`eventConditions`) = '{}'
           THEN @battle_notice_event
           ELSE `eventConditions`
       END
 WHERE `id` IN (2100301, 2102305)
   AND `classPath` <> '';

INSERT IGNORE INTO `server_battlenpc_pools`
  (`poolId`, `actorClassId`, `name`, `genusId`, `currentJob`, `combatSkill`,
   `combatDelay`, `combatDmgMult`, `aggroType`, `immunity`, `linkType`,
   `spellListId`, `skillListId`)
VALUES
  (201, 2100301, 'antelope_doe', 2, 0, 1, 4200, 1, 0, 0, 0, 0, 0),
  (202, 2102305, 'aldgoat_nanny', 1, 0, 1, 4200, 1, 0, 0, 0, 0, 0);

INSERT IGNORE INTO `server_battlenpc_groups`
  (`groupId`, `poolId`, `scriptName`, `minLevel`, `maxLevel`, `respawnTime`,
   `hp`, `mp`, `dropListId`, `allegiance`, `spawnType`, `animationId`,
   `actorState`, `privateAreaName`, `privateAreaLevel`, `zoneId`)
VALUES
  (201, 201, 'antelope_doe', 3, 6, 30, 0, 0, 0, 0, 0, 0, 0, '', 0, 170),
  (202, 202, 'aldgoat_nanny', 4, 7, 30, 0, 0, 0, 0, 0, 0, 0, '', 0, 170);

INSERT IGNORE INTO `server_battlenpc_spawn_locations`
  (`bnpcId`, `customDisplayName`, `groupId`, `positionX`, `positionY`, `positionZ`, `rotation`)
VALUES
  (10101, '', 201, 1386.500, 256.280, 73.250, 0.771),
  (10102, '', 201, 1381.750, 256.280, 77.500, -0.900),
  (10201, '', 202, 1378.900, 256.101, 72.297, -0.342),
  (10202, '', 202, 1375.500, 256.100, 69.750, 1.200);

INSERT IGNORE INTO `server_battlenpc_restoration_evidence`
  (`evidenceKey`, `subjectType`, `subjectId`, `zoneId`, `evidenceStatus`, `sourceType`, `sourceRef`, `notes`)
VALUES
  ('zone170-pack-20260701', 'zone-pack', 170, 170, 'provisional', 'server-trace', 'map-20260630-231920.jsonl', 'First Central Thanalan durable restoration pack; coordinates remain provisional.'),
  ('actor-2100301-classpath', 'actorClass', 2100301, 170, 'repo-confirmed', 'gamedata_actor_class', '2100301', 'Antelope doe has nonblank SerowFemaleStandard monster class path and appearance data.'),
  ('actor-2100301-preview', 'actorClass', 2100301, 170, 'trace-confirmed', 'server-trace', 'map-20260630-231920.jsonl', 'Live !spawn resolved SerowFemaleStandard base Lua in zone 170.'),
  ('actor-2102305-classpath', 'actorClass', 2102305, 170, 'repo-confirmed', 'gamedata_actor_class', '2102305', 'Aldgoat nanny has nonblank YakFemaleStandard monster class path and appearance data.'),
  ('actor-2102305-preview', 'actorClass', 2102305, 170, 'trace-confirmed', 'server-trace', 'map-20260630-231920.jsonl', 'Live !spawn resolved YakFemaleStandard base Lua in zone 170.'),
  ('pool-201-baseline', 'pool', 201, 170, 'provisional', 'repo-baseline', 'server_battlenpc_pools', 'Auto-attack-only Antelope pool using existing genus and durable BattleNpc loader.'),
  ('pool-202-baseline', 'pool', 202, 170, 'provisional', 'repo-baseline', 'server_battlenpc_pools', 'Auto-attack-only Aldgoat pool using existing genus and durable BattleNpc loader.'),
  ('spawn-10101-provisional', 'spawn', 10101, 170, 'provisional', 'server-trace', 'client.position 2026-06-30T23:22:19Z', 'Nearby field placement around traced antelope visual test position.'),
  ('spawn-10102-provisional', 'spawn', 10102, 170, 'provisional', 'server-trace', 'client.position 2026-06-30T23:22:19Z', 'Nearby field placement around traced antelope visual test position.'),
  ('spawn-10201-provisional', 'spawn', 10201, 170, 'provisional', 'server-trace', 'client.position 2026-06-30T23:22:45Z', 'Nearby field placement around traced aldgoat visual test position.'),
  ('spawn-10202-provisional', 'spawn', 10202, 170, 'provisional', 'server-trace', 'client.position 2026-06-30T23:22:45Z', 'Nearby field placement around traced aldgoat visual test position.'),
  ('pool-101-desert-rat-baseline', 'pool', 101, 170, 'provisional', 'repo-baseline', '20260627_central_thanalan_desert_rat_gate_test.sql', 'Existing Desert Rat test group is retained as a combat fixture/provisional field pack row.');
