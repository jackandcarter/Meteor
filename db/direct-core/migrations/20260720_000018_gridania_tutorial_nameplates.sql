START TRANSACTION;

-- The spawn labels are script identifiers, not player-facing names. Empty
-- custom names make SetActorName carry each actor class's localized name ID.
-- Reassert the recovered opening-battle roles as well as the labels. This makes
-- the repair converge databases that were seeded before migrations 16 and 17.
UPDATE `server_battlenpc_pools`
SET `actorClassId` = 2290006,
    `name` = 'yda'
WHERE `poolId` = 3;

UPDATE `server_battlenpc_pools`
SET `actorClassId` = 2290005,
    `name` = 'papalymo'
WHERE `poolId` = 4;

UPDATE `server_battlenpc_spawn_locations`
SET `customDisplayName` = ''
WHERE `bnpcId` IN (3, 4, 5, 6, 7);

COMMIT;
