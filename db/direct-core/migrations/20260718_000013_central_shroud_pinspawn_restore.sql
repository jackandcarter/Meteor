-- Import only the Central Shroud pinspawn work from the supplied 2026-07-18
-- database dump.  Account, character, and all other dump data are intentionally
-- excluded.  Creator character IDs are not portable between installations, so
-- the creator's recorded name is retained without creating a character FK.
--
-- Evidence boundary:
--   * Pins 1-33 predate patch 1.19's Central Shroud redistribution and remain
--     reference-only. Pins 29-31 are additionally Spore Spoor director actors.
--   * Pins 34-60 come from visibly post-1.19 "Logging Bentbranch" footage.
--     Only its Star Marmots are promoted: the 1.23b client export maps actor
--     classes 2104009/2104028 to "star marmot", and the official retail
--     gather_wood capture provides their zone-150 level/HP/MP presentation.
--   * Forest Funguar and Chigoe placements are retained until their exact
--     1.23b actor variant and battle profile are independently established.
--
-- Official capture: ffxiv_traces/gather_wood.pcapng
-- SHA-256: c308aa4c984eb3e1912383c06e5115c6924713978df43cdb8075335e1bac7d32

DROP TEMPORARY TABLE IF EXISTS `tmp_central_shroud_pinspawn`;
CREATE TEMPORARY TABLE `tmp_central_shroud_pinspawn` (
  `sourcePinId` INT UNSIGNED NOT NULL,
  `enemyName` VARCHAR(64) NOT NULL,
  `sourceNote` VARCHAR(255) NOT NULL,
  `zoneId` INT UNSIGNED NOT NULL,
  `positionX` FLOAT NOT NULL,
  `positionY` FLOAT NOT NULL,
  `positionZ` FLOAT NOT NULL,
  `rotation` FLOAT NOT NULL,
  `createdByCharacterName` VARCHAR(64) NOT NULL,
  `createdAt` TIMESTAMP NOT NULL,
  PRIMARY KEY (`sourcePinId`)
) ENGINE=InnoDB;

INSERT INTO `tmp_central_shroud_pinspawn`
  (`sourcePinId`, `enemyName`, `sourceNote`, `zoneId`, `positionX`, `positionY`, `positionZ`, `rotation`, `createdByCharacterName`, `createdAt`)
VALUES
  (1,'Star Marmot','youtube video',150,326.15,6.47209,-945.719,0.48482,'Akhebica Loha','2026-07-07 16:02:41'),
  (2,'Star Marmot','youtube https://www.youtube.com/watch?v=FBE5_Yyukr8 12:57',150,327.477,6.71234,-952.607,3.00482,'Akhebica Loha','2026-07-07 16:03:51'),
  (3,'Star Marmot','youtube https://www.youtube.com/watch?v=FBE5_Yyukr8 12:57',150,327.999,6.47348,-906.598,-0.513184,'Akhebica Loha','2026-07-07 16:04:58'),
  (4,'Star Marmot','youtube https://www.youtube.com/watch?v=FBE5_Yyukr8 12:57',150,366.925,7.70982,-903.26,0.816816,'Akhebica Loha','2026-07-07 16:05:10'),
  (5,'Star Marmot','youtube https://www.youtube.com/watch?v=FBE5_Yyukr8 12:57',150,382.424,6.28856,-918.229,2.15882,'Akhebica Loha','2026-07-07 16:05:39'),
  (6,'Star Marmot','youtube https://www.youtube.com/watch?v=FBE5_Yyukr8 12:57',150,387.192,6.65371,-881,-0.259179,'Akhebica Loha','2026-07-07 16:05:52'),
  (7,'Star Marmot','https://www.youtube.com/watch?v=b9tP2GW4pHc&t=33s 0:17',150,384.659,4.02071,-867.034,1.78701,'Akhebica Loha','2026-07-07 17:41:21'),
  (8,'Star Marmot','https://www.youtube.com/watch?v=b9tP2GW4pHc&t=33s 0:28',150,379.054,2.89883,-853.793,-1.71197,'Akhebica Loha','2026-07-07 17:42:04'),
  (9,'Star Marmot','https://www.youtube.com/watch?v=b9tP2GW4pHc&t=33s 0:34',150,383.258,0.998838,-841.079,0.232822,'Akhebica Loha','2026-07-07 17:45:17'),
  (10,'Star Marmot','https://www.youtube.com/watch?v=b9tP2GW4pHc&t=33s 0:34',150,388.68,-0.799997,-829.481,0.188822,'Akhebica Loha','2026-07-07 17:45:24'),
  (11,'Star Marmot','https://www.youtube.com/watch?v=b9tP2GW4pHc&t=33s 0:34',150,389.049,-0.300989,-824.927,0.0808164,'Akhebica Loha','2026-07-07 17:45:28'),
  (12,'Star Marmot','https://www.youtube.com/watch?v=b9tP2GW4pHc&t=33s 0:34',150,387.092,3.98605,-802.594,-0.129179,'Akhebica Loha','2026-07-07 17:45:50'),
  (13,'Forest Funguar','https://www.youtube.com/watch?v=b9tP2GW4pHc&t=33s 1:00',150,365.38,5.73726,-746.578,-0.429186,'Akhebica Loha','2026-07-07 17:47:31'),
  (14,'Forest Funguar','https://www.youtube.com/watch?v=b9tP2GW4pHc&t=33s 1:00',150,361.37,4.16782,-739.03,0.0482133,'Akhebica Loha','2026-07-07 17:47:44'),
  (15,'Forest Funguar','https://www.youtube.com/watch?v=b9tP2GW4pHc&t=33s 1:00',150,351.53,4,-735.2,-1.21518,'Akhebica Loha','2026-07-07 17:47:57'),
  (16,'Forest Funguar','https://www.youtube.com/watch?v=b9tP2GW4pHc&t=33s 1:00',150,373.623,5.08317,-715.559,0.783681,'Akhebica Loha','2026-07-07 17:48:08'),
  (17,'Forest Funguar','https://www.youtube.com/watch?v=b9tP2GW4pHc&t=33s 1:00',150,355.804,4.04819,-695.229,-0.50118,'Akhebica Loha','2026-07-07 17:48:49'),
  (18,'Forest Funguar','https://www.youtube.com/watch?v=b9tP2GW4pHc&t=33s 1:17',150,341.858,5.36775,-660.238,-0.551183,'Akhebica Loha','2026-07-07 17:49:05'),
  (19,'Forest Funguar','https://www.youtube.com/watch?v=b9tP2GW4pHc&t=33s 1:17',150,363.799,7.12599,-645.414,0.236818,'Akhebica Loha','2026-07-07 17:49:18'),
  (20,'Forest Funguar','https://www.youtube.com/watch?v=b9tP2GW4pHc&t=33s 1:17',150,320.788,5.40218,-657.92,-1.95919,'Akhebica Loha','2026-07-07 17:49:33'),
  (21,'Star Marmot','https://www.youtube.com/watch?v=b9tP2GW4pHc&t=33s 1:33',150,323.596,4.22434,-642.33,-0.88518,'Akhebica Loha','2026-07-07 17:50:23'),
  (22,'Star Marmot','youtube https://youtu.be/b9tP2GW4pHc?si=_URKr-jG57ZNPbyr&t=268',150,418.022,6.07131,-512.287,-2.93268,'Akhebica Loha','2026-07-17 19:18:02'),
  (23,'Forest Funguar','youtube https://youtu.be/b9tP2GW4pHc?si=_URKr-jG57ZNPbyr&t=268',150,435.372,5.63574,-501.177,1.02353,'Akhebica Loha','2026-07-17 19:18:32'),
  (24,'Forest Funguar','youtube https://youtu.be/b9tP2GW4pHc?si=OislaEVkMSvMwNb1&t=288',150,421.913,4.1054,-476.199,-0.701492,'Akhebica Loha','2026-07-17 19:19:47'),
  (25,'Forest Funguar','youtube https://youtu.be/b9tP2GW4pHc?si=OislaEVkMSvMwNb1&t=288',150,421.624,3.66652,-470.17,0.869303,'Akhebica Loha','2026-07-17 19:19:55'),
  (26,'Firefly','youtube https://youtu.be/b9tP2GW4pHc?si=OislaEVkMSvMwNb1&t=288',150,433.528,-0.0378631,-460.634,0.354512,'Akhebica Loha','2026-07-17 19:20:21'),
  (27,'Forest Funguar','https://youtu.be/b9tP2GW4pHc?si=wUltsRLxuZf1hwIc',150,376.856,3.40351,-585.098,0.302507,'Akhebica Loha','2026-07-17 19:21:48'),
  (28,'Forest Funguar','https://youtu.be/b9tP2GW4pHc?si=wUltsRLxuZf1hwIc',150,382.476,3.97688,-594.782,2.27451,'Akhebica Loha','2026-07-17 19:22:01'),
  (29,'Stumbling Funguar','Youtube, levequest Spore Spoor https://youtu.be/b9tP2GW4pHc?si=rVv7gPTlp5_mrCr9&t=343',150,472.025,16.1999,-574.21,-0.331082,'Akhebica Loha','2026-07-17 19:23:53'),
  (30,'Stumbling Funguar','Youtube, levequest Spore Spoor https://youtu.be/b9tP2GW4pHc?si=rVv7gPTlp5_mrCr9&t=343',150,482.315,16.2104,-571.697,1.18451,'Akhebica Loha','2026-07-17 19:24:04'),
  (31,'Stumbling Funguar','Youtube, levequest Spore Spoor https://youtu.be/b9tP2GW4pHc?si=rVv7gPTlp5_mrCr9&t=343',150,488.174,16.3171,-575.646,1.98051,'Akhebica Loha','2026-07-17 19:24:10'),
  (32,'Star Marmot','youtube https://youtu.be/b9tP2GW4pHc?si=rVv7gPTlp5_mrCr9&t=343',150,475.474,16.0427,-576.2,-1.15549,'Akhebica Loha','2026-07-17 19:24:39'),
  (33,'Star Marmot','youtube https://youtu.be/b9tP2GW4pHc?si=rVv7gPTlp5_mrCr9&t=343',150,484.589,16.1573,-573.364,-2.71429,'Akhebica Loha','2026-07-17 19:24:47'),
  (34,'Star Marmot','Youtube https://youtu.be/2ML3T0jvkFk?si=L3ZLjhuXNesxslKs&t=946',150,383.134,7.49095,-632.582,-0.730699,'Akhebica Loha','2026-07-17 19:36:32'),
  (35,'Star Marmot','Youtube https://youtu.be/2ML3T0jvkFk?si=L3ZLjhuXNesxslKs&t=946',150,379.14,7.04987,-642.835,-2.46749,'Akhebica Loha','2026-07-17 19:36:38'),
  (36,'Forest Funguar','youtube https://youtu.be/2ML3T0jvkFk?si=L3ZLjhuXNesxslKs&t=946',150,445.647,0.143259,-713.972,2.4057,'Akhebica Loha','2026-07-17 19:38:45'),
  (37,'Forest Funguar','youtube https://youtu.be/2ML3T0jvkFk?si=L3ZLjhuXNesxslKs&t=946',150,454.008,-0.530321,-718.771,2.0377,'Akhebica Loha','2026-07-17 19:38:53'),
  (38,'Forest Funguar','youtube https://youtu.be/2ML3T0jvkFk?si=L3ZLjhuXNesxslKs&t=946',150,449.661,-0.580559,-692.792,0.228304,'Akhebica Loha','2026-07-17 19:39:32'),
  (39,'Star Marmot','youtube https://youtu.be/2ML3T0jvkFk?si=DIrQp1jPilaqeLhf&t=1000',150,508.925,2.83839,-758.889,-3.13149,'Akhebica Loha','2026-07-17 19:40:48'),
  (40,'Forest Funguar','youtube https://youtu.be/2ML3T0jvkFk?si=DIrQp1jPilaqeLhf&t=1000',150,502.181,3.93661,-768.362,-2.28828,'Akhebica Loha','2026-07-17 19:41:11'),
  (41,'Star Marmot','Youtube https://youtu.be/2ML3T0jvkFk?si=k9HkKXQNYBkuRM_v&t=1015',150,565.307,7.89955,-757.057,1.53251,'Akhebica Loha','2026-07-17 19:42:33'),
  (42,'Star Marmot','https://youtu.be/2ML3T0jvkFk?si=-KuSv7HMDSOcu6tB&t=1019',150,583.624,12.8981,-773.228,-0.653491,'Akhebica Loha','2026-07-17 19:47:08'),
  (43,'Forest Funguar','Youtube https://youtu.be/2ML3T0jvkFk?si=bBfbgy9NDpBZ2aQD&t=1030',150,634.235,15.6364,-760.723,-1.2123,'Akhebica Loha','2026-07-17 19:48:16'),
  (44,'Forest Funguar','Youtube https://youtu.be/2ML3T0jvkFk?si=bBfbgy9NDpBZ2aQD&t=1030',150,641.232,15.2,-781.288,-1.48549,'Akhebica Loha','2026-07-17 19:48:27'),
  (45,'Chigoe','https://youtu.be/2ML3T0jvkFk?si=X6ks0LglUwByZbHc&t=1055',150,722.067,20.1045,-769.389,1.04251,'Akhebica Loha','2026-07-17 19:50:40'),
  (46,'Chigoe','https://youtu.be/2ML3T0jvkFk?si=X6ks0LglUwByZbHc&t=1055',150,764.218,20.6626,-759.565,1.31651,'Akhebica Loha','2026-07-17 19:50:54'),
  (47,'Chigoe','https://youtu.be/2ML3T0jvkFk?si=X6ks0LglUwByZbHc&t=1055',150,770.181,21.9262,-754.781,0.862507,'Akhebica Loha','2026-07-17 19:50:59'),
  (48,'Chigoe','https://youtu.be/2ML3T0jvkFk?si=X6ks0LglUwByZbHc&t=1055',150,780.137,20.4108,-766.99,2.23452,'Akhebica Loha','2026-07-17 19:51:09'),
  (49,'Chigoe','https://youtu.be/2ML3T0jvkFk?si=X6ks0LglUwByZbHc&t=1055',150,762.297,19.5918,-777.491,-1.94067,'Akhebica Loha','2026-07-17 19:51:18'),
  (50,'Chigoe','https://youtu.be/2ML3T0jvkFk?si=X6ks0LglUwByZbHc&t=1055',150,778.5,18.8,-781.051,2.05451,'Akhebica Loha','2026-07-17 19:51:26'),
  (51,'Star Marmot','Youtube https://youtu.be/2ML3T0jvkFk?si=5NdCN1LhNvWl2E82&t=1059',150,696.17,31.8713,-747.699,-2.4805,'Akhebica Loha','2026-07-17 19:54:56'),
  (52,'Forest Funguar','Youtube https://youtu.be/2ML3T0jvkFk?si=JGrRQTWu0F5-ORbD&t=1470',150,744.421,32,-872.575,3.04088,'Akhebica Loha','2026-07-17 20:01:10'),
  (53,'Forest Funguar','https://youtu.be/2ML3T0jvkFk?si=JGrRQTWu0F5-ORbD&t=1470',150,740.17,31.6159,-882.067,-2.7503,'Akhebica Loha','2026-07-17 20:01:26'),
  (54,'Forest Funguar','https://youtu.be/2ML3T0jvkFk?si=DR_Y3MSUW23rS0Cm&t=1474',150,761.182,31.625,-855.545,0.314478,'Akhebica Loha','2026-07-17 20:02:26'),
  (55,'Star Marmot','youtube https://youtu.be/2ML3T0jvkFk?si=DR_Y3MSUW23rS0Cm&t=1474',150,729.208,31.6149,-884.972,-0.243719,'Akhebica Loha','2026-07-17 20:03:01'),
  (56,'Star Marmot','https://youtu.be/2ML3T0jvkFk?si=1-wAfhOzDYtC1FwO&t=1498',150,834.204,32.07,-858.639,0.272888,'Akhebica Loha','2026-07-17 20:05:32'),
  (57,'Star Marmot','youtube https://youtu.be/2ML3T0jvkFk?si=MMVoGlj8Y5d2h3ov&t=2112',150,448.77,15.7075,-562.791,-2.63626,'Akhebica Loha','2026-07-17 20:16:24'),
  (58,'Star Marmot','youtube https://youtu.be/2ML3T0jvkFk?si=OQc92foiImbU5RSu&t=2142',150,512.85,16.512,-503.021,1.34772,'Akhebica Loha','2026-07-17 20:18:23'),
  (59,'Chigoe','youtube https://youtu.be/2ML3T0jvkFk?si=cvSTR7j56HfATrmV&t=2645',150,786.252,31.9082,-895.889,-1.86127,'Akhebica Loha','2026-07-17 20:26:35'),
  (60,'Star Marmot','Youtube, is paired with nearby Chigoe https://youtu.be/2ML3T0jvkFk?si=Hqj3sd0GD3OXnbj5&t=2658',150,803.961,32.1053,-892.631,1.31792,'Akhebica Loha','2026-07-17 20:27:45');

-- Converge matching local rows without overwriting a still-valid local creator
-- character ID. Matching is by the captured transform because source pin IDs
-- are installation-local AUTO_INCREMENT values.
UPDATE `server_battlenpc_spawn_audit_pins` AS p
JOIN `tmp_central_shroud_pinspawn` AS s
  ON p.`zoneId` = s.`zoneId`
 AND ABS(p.`positionX` - s.`positionX`) < 0.001
 AND ABS(p.`positionY` - s.`positionY`) < 0.001
 AND ABS(p.`positionZ` - s.`positionZ`) < 0.001
SET p.`enemyName` = s.`enemyName`,
    p.`sourceNote` = s.`sourceNote`,
    p.`createdByCharacterName` = s.`createdByCharacterName`,
    p.`createdAt` = s.`createdAt`,
    p.`isPromoted` = 0,
    p.`promotedAt` = NULL,
    p.`promotionMigration` = NULL,
    p.`promotionNote` = CASE
      WHEN s.`sourcePinId` BETWEEN 29 AND 31
        THEN CONCAT('Source dump pin #', s.`sourcePinId`, ': Spore Spoor director-owned enemy; never ambient.')
      WHEN s.`sourcePinId` <= 33
        THEN CONCAT('Source dump pin #', s.`sourcePinId`, ': retained only; footage predates patch 1.19 enemy redistribution.')
      WHEN s.`enemyName` = 'Star Marmot'
        THEN CONCAT('Source dump pin #', s.`sourcePinId`, ': post-1.19 Bentbranch placement; 1.23b identity/profile corroborated.')
      ELSE CONCAT('Source dump pin #', s.`sourcePinId`, ': post-1.19 placement retained; exact 1.23b actor/profile pending.')
    END;

INSERT INTO `server_battlenpc_spawn_audit_pins`
  (`enemyName`, `sourceNote`, `zoneId`, `positionX`, `positionY`, `positionZ`, `rotation`,
   `createdByCharacterId`, `createdByCharacterName`, `createdAt`, `isPromoted`, `promotedAt`, `promotionMigration`, `promotionNote`)
SELECT s.`enemyName`, s.`sourceNote`, s.`zoneId`, s.`positionX`, s.`positionY`, s.`positionZ`, s.`rotation`,
       NULL, s.`createdByCharacterName`, s.`createdAt`, 0, NULL, NULL,
       CASE
         WHEN s.`sourcePinId` BETWEEN 29 AND 31
           THEN CONCAT('Source dump pin #', s.`sourcePinId`, ': Spore Spoor director-owned enemy; never ambient.')
         WHEN s.`sourcePinId` <= 33
           THEN CONCAT('Source dump pin #', s.`sourcePinId`, ': retained only; footage predates patch 1.19 enemy redistribution.')
         WHEN s.`enemyName` = 'Star Marmot'
           THEN CONCAT('Source dump pin #', s.`sourcePinId`, ': post-1.19 Bentbranch placement; 1.23b identity/profile corroborated.')
         ELSE CONCAT('Source dump pin #', s.`sourcePinId`, ': post-1.19 placement retained; exact 1.23b actor/profile pending.')
       END
FROM `tmp_central_shroud_pinspawn` AS s
WHERE NOT EXISTS (
  SELECT 1
  FROM `server_battlenpc_spawn_audit_pins` AS p
  WHERE p.`zoneId` = s.`zoneId`
    AND ABS(p.`positionX` - s.`positionX`) < 0.001
    AND ABS(p.`positionY` - s.`positionY`) < 0.001
    AND ABS(p.`positionZ` - s.`positionZ`) < 0.001
);

-- Actor classes and localized names come from the exact 1.23b client export.
-- The combat defaults below are the unchanged direct-port BattleNpc defaults;
-- level 3, HP 99, and MP 130 are taken from the official zone-150 capture.
INSERT INTO `server_battlenpc_pools`
  (`poolId`, `actorClassId`, `name`, `genusId`, `currentJob`, `combatSkill`, `combatDelay`, `combatDmgMult`, `aggroType`, `immunity`, `linkType`, `spellListId`, `skillListId`)
VALUES
  (150001, 2104009, 'star_marmot_3104009', 12, 3, 1, 4200, 1, 0, 0, 0, 0, 0),
  (150002, 2104028, 'star_marmot_3104028', 12, 3, 1, 4200, 1, 0, 0, 0, 0, 0)
ON DUPLICATE KEY UPDATE
  `actorClassId` = VALUES(`actorClassId`), `name` = VALUES(`name`),
  `genusId` = VALUES(`genusId`), `currentJob` = VALUES(`currentJob`),
  `combatSkill` = VALUES(`combatSkill`), `combatDelay` = VALUES(`combatDelay`),
  `combatDmgMult` = VALUES(`combatDmgMult`), `aggroType` = VALUES(`aggroType`),
  `immunity` = VALUES(`immunity`), `linkType` = VALUES(`linkType`),
  `spellListId` = VALUES(`spellListId`), `skillListId` = VALUES(`skillListId`);

-- maxLevel is exclusive in the direct port. Respawn 10 is the legacy schema's
-- ambient default rather than a value synthesized from the capture.
INSERT INTO `server_battlenpc_groups`
  (`groupId`, `poolId`, `scriptName`, `minLevel`, `maxLevel`, `respawnTime`, `hp`, `mp`, `dropListId`, `allegiance`, `spawnType`, `animationId`, `actorState`, `privateAreaName`, `privateAreaLevel`, `zoneId`)
VALUES
  (1500101, 150001, 'star_marmot_3104009', 3, 4, 10, 99, 130, 0, 0, 0, 0, 0, '', 0, 150),
  (1500102, 150002, 'star_marmot_3104028', 3, 4, 10, 99, 130, 0, 0, 0, 0, 0, '', 0, 150)
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
  -- Exact retail capture positions and actor variants.
  (1500001, '', 1500101, 410.786163, 5.366178, -397.583344, 2.340096),
  (1500002, '', 1500102, 325.138397, 5.438738, -505.644379, 1.074152),
  -- Post-1.19 Logging Bentbranch pins with trace/client-confirmed species.
  (1500034, '', 1500101, 383.134, 7.49095, -632.582, -0.730699),
  (1500035, '', 1500101, 379.14, 7.04987, -642.835, -2.46749),
  (1500039, '', 1500101, 508.925, 2.83839, -758.889, -3.13149),
  (1500041, '', 1500101, 565.307, 7.89955, -757.057, 1.53251),
  (1500042, '', 1500101, 583.624, 12.8981, -773.228, -0.653491),
  (1500051, '', 1500101, 696.17, 31.8713, -747.699, -2.4805),
  (1500055, '', 1500101, 729.208, 31.6149, -884.972, -0.243719),
  (1500056, '', 1500101, 834.204, 32.07, -858.639, 0.272888),
  (1500057, '', 1500101, 448.77, 15.7075, -562.791, -2.63626),
  (1500058, '', 1500101, 512.85, 16.512, -503.021, 1.34772),
  (1500060, '', 1500101, 803.961, 32.1053, -892.631, 1.31792)
ON DUPLICATE KEY UPDATE
  `customDisplayName` = VALUES(`customDisplayName`), `groupId` = VALUES(`groupId`),
  `positionX` = VALUES(`positionX`), `positionY` = VALUES(`positionY`),
  `positionZ` = VALUES(`positionZ`), `rotation` = VALUES(`rotation`);

UPDATE `server_battlenpc_spawn_audit_pins` AS p
JOIN `tmp_central_shroud_pinspawn` AS s
  ON p.`zoneId` = s.`zoneId`
 AND ABS(p.`positionX` - s.`positionX`) < 0.001
 AND ABS(p.`positionY` - s.`positionY`) < 0.001
 AND ABS(p.`positionZ` - s.`positionZ`) < 0.001
SET p.`isPromoted` = 1,
    p.`promotedAt` = COALESCE(p.`promotedAt`, CURRENT_TIMESTAMP),
    p.`promotionMigration` = '20260718_000013_central_shroud_pinspawn_restore',
    p.`promotionNote` = CONCAT('Source dump pin #', s.`sourcePinId`, ': post-1.19 Bentbranch Star Marmot; 1.23b client and retail trace corroborated.')
WHERE s.`sourcePinId` IN (34,35,39,41,42,51,55,56,57,58,60);

DROP TEMPORARY TABLE `tmp_central_shroud_pinspawn`;
