-- Restore the 23 ambient battle NPCs observed in the official zone-162
-- captures.  This is static production data; captures are not consumed by
-- the runtime.  Guildleve-owned enemies remain director content and are not
-- inserted here.

-- Remove the four Central Thanalan Desert Rats that were added as manual
-- development fixtures.  The two original bnpcId 1/2 test actors are kept.
DELETE FROM `server_battlenpc_spawn_mods`
WHERE `bnpcId` IN (10001, 10002, 10003, 10004);
DELETE FROM `server_battlenpc_spawn_locations`
WHERE `bnpcId` IN (10001, 10002, 10003, 10004);
DELETE FROM `server_battlenpc_groups`
WHERE `groupId` = 101
  AND NOT EXISTS (
    SELECT 1 FROM `server_battlenpc_spawn_locations` WHERE `groupId` = 101
  );
DELETE FROM `server_battlenpc_pool_mods`
WHERE `poolId` = 101
  AND NOT EXISTS (
    SELECT 1 FROM `server_battlenpc_groups` WHERE `poolId` = 101
  );
DELETE FROM `server_battlenpc_pools`
WHERE `poolId` = 101
  AND NOT EXISTS (
    SELECT 1 FROM `server_battlenpc_groups` WHERE `poolId` = 101
  );

-- These actor presentations are taken directly from their 0x00CC bindings.
-- All observed ambient enemies expose properties 0, 1, 2, and 4 and the
-- standard notice event, represented by propertyFlags 23.
UPDATE `gamedata_actor_class`
SET `classPath` = CASE `id`
    WHEN 2100504 THEN '/Chara/Npc/Monster/Monkey/MonkeyLesserStandard'
    WHEN 2101424 THEN '/Chara/Npc/Monster/Wolf/WolfStandard'
    WHEN 2102708 THEN '/Chara/Npc/Monster/Flower/FlowerStandard'
    WHEN 2102721 THEN '/Chara/Npc/Monster/Flower/FlowerPoisonousStandard'
    WHEN 2103905 THEN '/Chara/Npc/Monster/Bug/LadybugStandard'
    WHEN 2104007 THEN '/Chara/Npc/Monster/Lemming/NuteaterStandard'
    WHEN 2104017 THEN '/Chara/Npc/Monster/Lemming/GlirulusStandard'
    WHEN 2104105 THEN '/Chara/Npc/Monster/Bat/BatNormalStandard'
    WHEN 2107606 THEN '/Chara/Npc/Monster/Crab/CrabLesserStandard'
    ELSE `classPath`
  END,
  `propertyFlags` = 23,
  `eventConditions` = '{\r\n  "talkEventConditions": [],\r\n  "noticeEventConditions": [\r\n    {\r\n      "unknown1": 0,\r\n      "unknown2": 1,\r\n      "conditionName": "noticeEvent"\r\n    }\r\n  ],\r\n  "emoteEventConditions": [],\r\n  "pushWithCircleEventConditions": []\r\n}'
WHERE `id` IN (2100504, 2101424, 2102708, 2102721, 2103905, 2104007, 2104017, 2104105, 2107606);

INSERT INTO `server_battlenpc_pools`
  (`poolId`, `actorClassId`, `name`, `genusId`, `currentJob`, `combatSkill`, `combatDelay`, `combatDmgMult`, `aggroType`, `immunity`, `linkType`, `spellListId`, `skillListId`)
VALUES
  (162001, 2104105, 'bat_normal_standard',       43, 3, 1, 4200, 1, 0, 0, 0, 0, 0),
  (162002, 2103905, 'ladybug_standard',          38, 3, 1, 4200, 1, 0, 0, 0, 0, 0),
  (162003, 2102721, 'flower_poisonous_standard', 15, 7, 1, 4200, 1, 0, 0, 0, 0, 0),
  (162004, 2101424, 'wolf_standard',              3, 3, 1, 4200, 1, 0, 0, 0, 0, 0),
  (162005, 2104007, 'nuteater_standard',         12, 3, 1, 4200, 1, 0, 0, 0, 0, 0),
  (162006, 2104017, 'glirulus_standard',         12, 3, 1, 4200, 1, 0, 0, 0, 0, 0),
  (162007, 2107606, 'crab_lesser_standard',      22, 3, 1, 4200, 1, 0, 0, 0, 0, 0),
  (162008, 2102708, 'flower_standard',           15, 7, 1, 4200, 1, 0, 0, 0, 0, 0),
  (162009, 2100504, 'monkey_lesser_standard',     4, 7, 1, 4200, 1, 0, 0, 0, 0, 0)
ON DUPLICATE KEY UPDATE
  `actorClassId` = VALUES(`actorClassId`),
  `name` = VALUES(`name`),
  `genusId` = VALUES(`genusId`),
  `currentJob` = VALUES(`currentJob`),
  `combatSkill` = VALUES(`combatSkill`),
  `combatDelay` = VALUES(`combatDelay`),
  `combatDmgMult` = VALUES(`combatDmgMult`),
  `aggroType` = VALUES(`aggroType`),
  `immunity` = VALUES(`immunity`),
  `linkType` = VALUES(`linkType`),
  `spellListId` = VALUES(`spellListId`),
  `skillListId` = VALUES(`skillListId`);

-- maxLevel is exclusive in the direct port, so N..N+1 fixes the observed
-- level while hp/mp preserve the corresponding retail base-init values.
INSERT INTO `server_battlenpc_groups`
  (`groupId`, `poolId`, `scriptName`, `minLevel`, `maxLevel`, `respawnTime`, `hp`, `mp`, `dropListId`, `allegiance`, `spawnType`, `animationId`, `actorState`, `privateAreaName`, `privateAreaLevel`, `zoneId`)
VALUES
  (1620101, 162001, 'trace_zone162_bat_26',       26, 27, 10,  555,  640, 0, 0, 0, 0, 0, '', 0, 162),
  (1620102, 162001, 'trace_zone162_bat_25',       25, 26, 10,  520,  600, 0, 0, 0, 0, 0, '', 0, 162),
  (1620103, 162001, 'trace_zone162_bat_28',       28, 29, 10,  633,  720, 0, 0, 0, 0, 0, '', 0, 162),
  (1620201, 162002, 'trace_zone162_ladybug_27',   27, 28, 10,  644,  680, 0, 0, 0, 0, 0, '', 0, 162),
  (1620202, 162002, 'trace_zone162_ladybug_29',   29, 30, 10,  735,  760, 0, 0, 0, 0, 0, '', 0, 162),
  (1620203, 162002, 'trace_zone162_ladybug_26',   26, 27, 10,  603,  640, 0, 0, 0, 0, 0, '', 0, 162),
  (1620301, 162003, 'trace_zone162_poison_31',    31, 32, 10, 1007,  870, 0, 0, 0, 0, 0, '', 0, 162),
  (1620401, 162004, 'trace_zone162_wolf_32',      32, 33, 10,  826,  940, 0, 0, 0, 0, 0, '', 0, 162),
  (1620501, 162005, 'trace_zone162_nuteater_32',  32, 33, 10, 1077,  940, 0, 0, 0, 0, 0, '', 0, 162),
  (1620601, 162006, 'trace_zone162_glirulus_39',  39, 40, 10, 1711, 1430, 0, 0, 0, 0, 0, '', 0, 162),
  (1620701, 162007, 'trace_zone162_crab_32',      32, 33, 10,  897,  940, 0, 0, 0, 0, 0, '', 0, 162),
  (1620801, 162008, 'trace_zone162_flower_27',    27, 28, 10,  773,  680, 0, 0, 0, 0, 0, '', 0, 162),
  (1620802, 162008, 'trace_zone162_flower_26',    26, 27, 10,  724,  640, 0, 0, 0, 0, 0, '', 0, 162),
  (1620803, 162008, 'trace_zone162_flower_25',    25, 26, 10,  679,  600, 0, 0, 0, 0, 0, '', 0, 162),
  (1620804, 162008, 'trace_zone162_flower_29',    29, 30, 10,  883,  760, 0, 0, 0, 0, 0, '', 0, 162),
  (1620901, 162009, 'trace_zone162_monkey_31',    31, 32, 10,  839,  870, 0, 0, 0, 0, 0, '', 0, 162)
ON DUPLICATE KEY UPDATE
  `poolId` = VALUES(`poolId`), `scriptName` = VALUES(`scriptName`),
  `minLevel` = VALUES(`minLevel`), `maxLevel` = VALUES(`maxLevel`),
  `respawnTime` = VALUES(`respawnTime`), `hp` = VALUES(`hp`), `mp` = VALUES(`mp`),
  `dropListId` = VALUES(`dropListId`), `allegiance` = VALUES(`allegiance`),
  `spawnType` = VALUES(`spawnType`), `animationId` = VALUES(`animationId`),
  `actorState` = VALUES(`actorState`), `privateAreaName` = VALUES(`privateAreaName`),
  `privateAreaLevel` = VALUES(`privateAreaLevel`), `zoneId` = VALUES(`zoneId`);

INSERT INTO `server_battlenpc_spawn_locations`
  (`bnpcId`, `customDisplayName`, `groupId`, `positionX`, `positionY`, `positionZ`, `rotation`)
VALUES
  (1620001, '', 1620101,  -61.16958, -7.78991, -591.18054,  0.97327),
  (1620002, '', 1620102,  -81.43601, -8.20899, -573.87634, -1.20194),
  (1620003, '', 1620103,  -62.91187, -7.78991, -594.78003,  2.73169),
  (1620004, '', 1620201,  194.00171, 16.79252, -419.24710, -3.02558),
  (1620005, '', 1620202,   48.28629, 16.96348, -393.86533, -0.58366),
  (1620006, '', 1620203,  126.95948, 17.02529, -420.41779, -2.49736),
  (1620007, '', 1620202,    2.31962,  4.61827, -569.17352,  1.85291),
  (1620008, '', 1620301,  -61.16231, -3.38988, -440.25916,  2.06776),
  (1620009, '', 1620401,  -66.43808,  5.72657, -492.18704,  3.12484),
  (1620010, '', 1620401,  -63.73685,  5.02166, -484.55063,  0.37315),
  (1620011, '', 1620401,  -57.51696,  2.20651, -468.47250, -2.81456),
  (1620012, '', 1620401,  -64.36862,  4.02495, -478.64957, -0.32335),
  (1620013, '', 1620501, -170.86923,  4.43841, -568.65656, -0.61096),
  (1620014, '', 1620501,   37.92091, 17.01321, -385.55667, -0.98846),
  (1620015, '', 1620601,   96.69894, 16.53277, -497.44562,  1.71833),
  (1620016, '', 1620601,   90.80339, 16.78352, -483.06247, -2.85774),
  (1620017, '', 1620701, -125.93647,  4.63059, -618.49994, -3.09493),
  (1620018, '', 1620801,  139.30753, 16.80289, -527.03851,  0.60771),
  (1620019, '', 1620802,  138.90263, 16.54540, -520.61646,  2.88084),
  (1620020, '', 1620803,   64.52616, 16.39093, -518.27167, -2.67213),
  (1620021, '', 1620804,    4.98406, 15.94637, -509.03909,  0.52699),
  (1620022, '', 1620802,    8.99056, 16.46906, -515.19415,  1.66801),
  (1620023, '', 1620901,  -64.70834, -3.68286, -440.08371,  2.85307)
ON DUPLICATE KEY UPDATE
  `customDisplayName` = VALUES(`customDisplayName`),
  `groupId` = VALUES(`groupId`),
  `positionX` = VALUES(`positionX`), `positionY` = VALUES(`positionY`),
  `positionZ` = VALUES(`positionZ`), `rotation` = VALUES(`rotation`);
