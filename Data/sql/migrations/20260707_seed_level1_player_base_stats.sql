-- Fill missing level 1 race baseline stats so new characters do not enter
-- combat with zero STR/VIT/DEX/INT/MND/PIE when upgraded databases have an
-- empty server_player_base_stats table. INSERT IGNORE preserves any stronger
-- client/trace-confirmed rows that are added later.

CREATE TABLE IF NOT EXISTS `server_player_base_stats` (
  `classId` tinyint(3) unsigned NOT NULL,
  `tribe` tinyint(3) unsigned NOT NULL DEFAULT '0',
  `level` smallint(6) NOT NULL,
  `hp` smallint(6) NOT NULL DEFAULT '0',
  `mp` smallint(6) NOT NULL DEFAULT '0',
  `str` smallint(6) NOT NULL DEFAULT '0',
  `vit` smallint(6) NOT NULL DEFAULT '0',
  `dex` smallint(6) NOT NULL DEFAULT '0',
  `int` smallint(6) NOT NULL DEFAULT '0',
  `mnd` smallint(6) NOT NULL DEFAULT '0',
  `pie` smallint(6) NOT NULL DEFAULT '0',
  `source` varchar(255) DEFAULT NULL,
  `sourceConfidence` enum('client-confirmed','trace-confirmed','public-confirmed','hypothesis') NOT NULL DEFAULT 'hypothesis',
  PRIMARY KEY (`classId`,`tribe`,`level`)
) ENGINE=InnoDB DEFAULT CHARSET=latin1;

INSERT IGNORE INTO `server_player_base_stats`
  (`classId`, `tribe`, `level`, `hp`, `mp`, `str`, `vit`, `dex`, `int`, `mnd`, `pie`, `source`, `sourceConfidence`)
SELECT
  playable.`classId`,
  baseline.`tribe`,
  1,
  0,
  0,
  baseline.`str`,
  baseline.`vit`,
  baseline.`dex`,
  baseline.`int`,
  baseline.`mnd`,
  baseline.`pie`,
  'user-provided 1.23 race baseline notes',
  'public-confirmed'
FROM (
  SELECT 2 AS `classId` UNION ALL
  SELECT 3 UNION ALL
  SELECT 4 UNION ALL
  SELECT 7 UNION ALL
  SELECT 8 UNION ALL
  SELECT 22 UNION ALL
  SELECT 23 UNION ALL
  SELECT 29 UNION ALL
  SELECT 30 UNION ALL
  SELECT 31 UNION ALL
  SELECT 32 UNION ALL
  SELECT 33 UNION ALL
  SELECT 34 UNION ALL
  SELECT 35 UNION ALL
  SELECT 36 UNION ALL
  SELECT 39 UNION ALL
  SELECT 40 UNION ALL
  SELECT 41
) AS playable
JOIN (
  SELECT 1 AS `tribe`, 22 AS `str`, 19 AS `vit`, 20 AS `dex`, 23 AS `int`, 19 AS `mnd`, 17 AS `pie` UNION ALL
  SELECT 2, 22, 19, 20, 23, 19, 17 UNION ALL
  SELECT 3, 23, 20, 22, 18, 20, 17 UNION ALL
  SELECT 4, 20, 23, 19, 22, 19, 17 UNION ALL
  SELECT 5, 20, 23, 19, 22, 19, 17 UNION ALL
  SELECT 6, 20, 20, 19, 23, 21, 17 UNION ALL
  SELECT 7, 20, 20, 19, 23, 21, 17 UNION ALL
  SELECT 8, 19, 23, 19, 22, 20, 17 UNION ALL
  SELECT 9, 19, 23, 19, 22, 20, 17 UNION ALL
  SELECT 10, 19, 21, 18, 22, 23, 17 UNION ALL
  SELECT 11, 19, 21, 18, 22, 23, 17 UNION ALL
  SELECT 12, 22, 21, 20, 19, 19, 17 UNION ALL
  SELECT 13, 22, 21, 20, 19, 19, 17 UNION ALL
  SELECT 14, 19, 22, 18, 21, 23, 17 UNION ALL
  SELECT 15, 19, 22, 18, 21, 23, 17 UNION ALL
  SELECT 16, 22, 19, 23, 18, 21, 17 UNION ALL
  SELECT 17, 22, 19, 23, 18, 21, 17 UNION ALL
  SELECT 18, 20, 18, 23, 20, 22, 17 UNION ALL
  SELECT 19, 20, 18, 23, 20, 22, 17
) AS baseline;
