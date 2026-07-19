-- A single machine-readable compatibility contract checked before any service starts.
CREATE TABLE IF NOT EXISTS `aether_database_compatibility` (
  `compatibility_key` varchar(32) NOT NULL,
  `schema_generation` int unsigned NOT NULL,
  `schema_version` int unsigned NOT NULL,
  `compatibility_id` varchar(96) NOT NULL,
  `baseline_id` varchar(96) NOT NULL,
  `minimum_core_version` varchar(32) NOT NULL,
  `installed_at` timestamp NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `updated_at` timestamp NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  PRIMARY KEY (`compatibility_key`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8;

INSERT INTO `aether_database_compatibility`
  (`compatibility_key`, `schema_generation`, `schema_version`, `compatibility_id`, `baseline_id`, `minimum_core_version`)
VALUES
  ('direct-core', 2, 1, 'aetherxiv-direct-core-v2', '20260716_000001_ffxiv_server_v2_baseline', '2.0')
ON DUPLICATE KEY UPDATE
  `schema_generation` = VALUES(`schema_generation`),
  `schema_version` = VALUES(`schema_version`),
  `compatibility_id` = VALUES(`compatibility_id`),
  `baseline_id` = VALUES(`baseline_id`),
  `minimum_core_version` = VALUES(`minimum_core_version`),
  `updated_at` = CURRENT_TIMESTAMP;
