START TRANSACTION;

-- Converge the complete runtime lookup chain used by
-- WorldManager.SpawnBattleNpcById(6/7). Updating only actorClassId is not
-- sufficient when an older database also has stale spawn/group links or
-- labels, so each canonical row is upserted in full.
INSERT INTO `server_battlenpc_pools`
    (`poolId`, `actorClassId`, `name`, `genusId`, `currentJob`, `combatSkill`,
     `combatDelay`, `combatDmgMult`, `aggroType`, `immunity`, `linkType`,
     `spellListId`, `skillListId`)
VALUES
    (3, 2290006, 'yda',      29,  2, 1, 4200, 1, 0, 0, 0,     0, 30010),
    (4, 2290005, 'papalymo', 29, 22, 1, 4200, 1, 0, 0, 0, 30011,     0)
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

INSERT INTO `server_battlenpc_groups`
    (`groupId`, `poolId`, `scriptName`, `minLevel`, `maxLevel`, `respawnTime`,
     `hp`, `mp`, `dropListId`, `allegiance`, `spawnType`, `animationId`,
     `actorState`, `privateAreaName`, `privateAreaLevel`, `zoneId`)
VALUES
    (3, 3, 'yda',      1, 1, 0, 0, 0, 0, 1, 1, 0, 0, '', 0, 166),
    (4, 4, 'papalymo', 1, 1, 0, 0, 0, 0, 1, 1, 0, 0, '', 0, 166)
ON DUPLICATE KEY UPDATE
    `poolId` = VALUES(`poolId`),
    `scriptName` = VALUES(`scriptName`),
    `minLevel` = VALUES(`minLevel`),
    `maxLevel` = VALUES(`maxLevel`),
    `respawnTime` = VALUES(`respawnTime`),
    `hp` = VALUES(`hp`),
    `mp` = VALUES(`mp`),
    `dropListId` = VALUES(`dropListId`),
    `allegiance` = VALUES(`allegiance`),
    `spawnType` = VALUES(`spawnType`),
    `animationId` = VALUES(`animationId`),
    `actorState` = VALUES(`actorState`),
    `privateAreaName` = VALUES(`privateAreaName`),
    `privateAreaLevel` = VALUES(`privateAreaLevel`),
    `zoneId` = VALUES(`zoneId`);

INSERT INTO `server_battlenpc_spawn_locations`
    (`bnpcId`, `customDisplayName`, `groupId`, `positionX`, `positionY`,
     `positionZ`, `rotation`)
VALUES
    (6, 'yda',      3, 365.266, 4.1220, -700.730,  1.5659),
    (7, 'papalymo', 4, 365.890, 4.0943, -706.720, -0.7180)
ON DUPLICATE KEY UPDATE
    `customDisplayName` = VALUES(`customDisplayName`),
    `groupId` = VALUES(`groupId`),
    `positionX` = VALUES(`positionX`),
    `positionY` = VALUES(`positionY`),
    `positionZ` = VALUES(`positionZ`),
    `rotation` = VALUES(`rotation`);

COMMIT;
