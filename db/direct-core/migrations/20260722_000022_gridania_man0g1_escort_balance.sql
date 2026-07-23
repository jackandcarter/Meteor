-- Preserve migration 21 for databases that already applied the first escort
-- implementation. Contemporary retail documentation identifies ankle biters
-- as scripted 3-HP targets, including for non-combat classes.
START TRANSACTION;

UPDATE `server_battlenpc_groups`
SET `hp`=3
WHERE `groupId`=122
  AND `poolId`=122
  AND `scriptName`='ankle_biter_gridania'
  AND `zoneId`=150;

COMMIT;
