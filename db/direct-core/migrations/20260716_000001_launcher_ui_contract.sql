-- Modern AetherXIV Launcher/UI storage contract on the direct-core database.
-- Gameplay tables remain untouched. The imported 1.3 launcher tables are
-- retained with a _v13 suffix as source evidence.

SET @has_modern_contract := (
  SELECT COUNT(*) FROM information_schema.columns
  WHERE table_schema=DATABASE() AND table_name='launcher_config' AND column_name='service_version'
);

SET @sql := IF(@has_modern_contract=0, 'RENAME TABLE `launcher_config` TO `launcher_config_v13`', 'DO 0');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;
SET @sql := IF(@has_modern_contract=0, 'RENAME TABLE `launcher_news` TO `launcher_news_v13`', 'DO 0');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;
SET @sql := IF(@has_modern_contract=0, 'RENAME TABLE `launcher_patch_files` TO `launcher_patch_files_v13`', 'DO 0');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;
SET @sql := IF(@has_modern_contract=0, 'RENAME TABLE `launcher_runtime_artifacts` TO `launcher_runtime_artifacts_v13`', 'DO 0');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;
SET @sql := IF(@has_modern_contract=0, 'RENAME TABLE `launcher_umbra_framework_artifacts` TO `launcher_umbra_framework_artifacts_v13`', 'DO 0');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;
SET @sql := IF(@has_modern_contract=0, 'RENAME TABLE `launcher_umbra_plugin_repositories` TO `launcher_umbra_plugin_repositories_v13`', 'DO 0');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;
SET @sql := IF(@has_modern_contract=0, 'RENAME TABLE `launcher_umbra_plugin_releases` TO `launcher_umbra_plugin_releases_v13`', 'DO 0');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;
SET @sql := IF(@has_modern_contract=0, 'RENAME TABLE `launcher_umbra_plugin_blocks` TO `launcher_umbra_plugin_blocks_v13`', 'DO 0');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

CREATE TABLE IF NOT EXISTS `launcher_config` (
  `config_key` varchar(64) NOT NULL,
  `service_version` int unsigned NOT NULL DEFAULT 1,
  `server_name` varchar(128) NOT NULL,
  `server_status_url` varchar(255) DEFAULT NULL,
  `news_url` varchar(255) NOT NULL,
  `patch_manifest_url` varchar(255) NOT NULL,
  `runtime_catalog_url` varchar(255) DEFAULT NULL,
  `login_url` varchar(255) DEFAULT NULL,
  `account_create_url` varchar(255) DEFAULT NULL,
  `client_login_url` varchar(255) DEFAULT NULL,
  `patch_base_url` varchar(255) DEFAULT NULL,
  `target_boot_version` varchar(32) NOT NULL,
  `target_game_version` varchar(32) NOT NULL,
  `client_plugin_framework_catalog_url` varchar(255) DEFAULT NULL,
  `plugin_blocklist_url` varchar(255) DEFAULT NULL,
  `is_active` tinyint(1) NOT NULL DEFAULT 1,
  `updated_at` timestamp NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  PRIMARY KEY (`config_key`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS `launcher_config_plugin_catalogs` (
  `config_key` varchar(64) NOT NULL,
  `catalog_url` varchar(255) NOT NULL,
  `sort_order` int NOT NULL DEFAULT 0,
  PRIMARY KEY (`config_key`, `catalog_url`),
  CONSTRAINT `fk_launcher_config_plugin_catalogs_config`
    FOREIGN KEY (`config_key`) REFERENCES `launcher_config` (`config_key`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS `launcher_status` (
  `status_key` varchar(64) NOT NULL,
  `state` varchar(32) NOT NULL,
  `message` varchar(255) NOT NULL,
  `updated_at` timestamp NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  PRIMARY KEY (`status_key`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS `launcher_news` (
  `news_id` int unsigned NOT NULL AUTO_INCREMENT,
  `title` varchar(160) NOT NULL,
  `summary` varchar(500) NOT NULL,
  `body` text DEFAULT NULL,
  `banner_url` varchar(255) DEFAULT NULL,
  `link_url` varchar(255) DEFAULT NULL,
  `published_at` timestamp NOT NULL,
  `is_active` tinyint(1) NOT NULL DEFAULT 1,
  `sort_order` int NOT NULL DEFAULT 0,
  `title_color` varchar(9) NOT NULL DEFAULT '#F2F4FA',
  `summary_color` varchar(9) NOT NULL DEFAULT '#D6DCE3',
  `body_color` varchar(9) NOT NULL DEFAULT '#AEB7C2',
  `created_at` timestamp NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (`news_id`),
  KEY `idx_launcher_news_active_publish` (`is_active`, `published_at`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS `launcher_presentation` (
  `presentation_key` varchar(64) NOT NULL,
  `reel_text_enabled` tinyint(1) NOT NULL DEFAULT 0,
  `updated_at` timestamp NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  PRIMARY KEY (`presentation_key`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS `launcher_reel_text` (
  `image_file` varchar(255) NOT NULL,
  `header_text` varchar(160) NOT NULL DEFAULT '',
  `sub_text` varchar(300) NOT NULL DEFAULT '',
  `header_size` decimal(5,1) NOT NULL DEFAULT 32.0,
  `sub_text_size` decimal(5,1) NOT NULL DEFAULT 18.0,
  `header_color` varchar(9) NOT NULL DEFAULT '#FFFFFFFF',
  `sub_text_color` varchar(9) NOT NULL DEFAULT '#FFD7E0EE',
  `is_enabled` tinyint(1) NOT NULL DEFAULT 1,
  `updated_at` timestamp NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  PRIMARY KEY (`image_file`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS `launcher_patch_files` (
  `patch_file_id` bigint unsigned NOT NULL AUTO_INCREMENT,
  `target_boot_version` varchar(32) NOT NULL,
  `target_game_version` varchar(32) NOT NULL,
  `relative_path` varchar(255) NOT NULL,
  `size_bytes` bigint NOT NULL DEFAULT 0,
  `crc32` char(8) NOT NULL,
  `sha256` char(64) DEFAULT NULL,
  `is_active` tinyint(1) NOT NULL DEFAULT 1,
  `sort_order` int NOT NULL DEFAULT 0,
  `created_at` timestamp NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (`patch_file_id`),
  UNIQUE KEY `uq_launcher_patch_file` (`target_boot_version`, `target_game_version`, `relative_path`),
  KEY `idx_launcher_patch_file_target` (`target_boot_version`, `target_game_version`, `is_active`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS `launcher_runtime_artifacts` (
  `artifact_id` int unsigned NOT NULL AUTO_INCREMENT,
  `name` varchar(128) NOT NULL,
  `version` varchar(64) NOT NULL,
  `platform_rid` varchar(64) NOT NULL,
  `runtime_kind` varchar(64) NOT NULL,
  `archive_url` varchar(512) NOT NULL,
  `archive_format` varchar(32) NOT NULL,
  `size_bytes` bigint NOT NULL DEFAULT 0,
  `sha256` char(64) NOT NULL,
  `executable_relative_path` varchar(255) NOT NULL,
  `prefix_arch` varchar(32) NOT NULL,
  `environment_json` json NOT NULL,
  `is_default` tinyint(1) NOT NULL DEFAULT 0,
  `is_active` tinyint(1) NOT NULL DEFAULT 1,
  `sort_order` int NOT NULL DEFAULT 0,
  `created_at` timestamp NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `updated_at` timestamp NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  PRIMARY KEY (`artifact_id`),
  UNIQUE KEY `uq_launcher_runtime_artifact` (`platform_rid`, `name`, `version`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS `launcher_umbra_framework_artifacts` (
  `artifact_id` int unsigned NOT NULL AUTO_INCREMENT,
  `name` varchar(128) NOT NULL,
  `version` varchar(64) NOT NULL,
  `api_version` varchar(64) NOT NULL,
  `platform_rid` varchar(64) NOT NULL,
  `archive_url` varchar(512) NOT NULL,
  `archive_format` varchar(32) NOT NULL,
  `size_bytes` bigint NOT NULL DEFAULT 0,
  `sha256` char(64) NOT NULL,
  `bootstrap_relative_path` varchar(255) NOT NULL,
  `framework_relative_path` varchar(255) NOT NULL,
  `supported_game_sha256_json` json NOT NULL,
  `is_default` tinyint(1) NOT NULL DEFAULT 0,
  `is_active` tinyint(1) NOT NULL DEFAULT 1,
  `sort_order` int NOT NULL DEFAULT 0,
  `created_at` timestamp NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `updated_at` timestamp NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  PRIMARY KEY (`artifact_id`),
  UNIQUE KEY `uq_launcher_umbra_framework_artifact` (`platform_rid`, `name`, `version`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS `launcher_umbra_plugin_repositories` (
  `repository_id` int unsigned NOT NULL AUTO_INCREMENT,
  `repository_key` varchar(96) NOT NULL,
  `repository_name` varchar(128) NOT NULL,
  `catalog_url` varchar(255) NOT NULL,
  `is_active` tinyint(1) NOT NULL DEFAULT 1,
  `sort_order` int NOT NULL DEFAULT 0,
  `created_at` timestamp NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `updated_at` timestamp NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  PRIMARY KEY (`repository_id`),
  UNIQUE KEY `uq_launcher_umbra_plugin_repository_key` (`repository_key`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS `launcher_umbra_plugins` (
  `plugin_id` int unsigned NOT NULL AUTO_INCREMENT,
  `repository_id` int unsigned NOT NULL,
  `plugin_key` varchar(128) NOT NULL,
  `name` varchar(128) NOT NULL,
  `version` varchar(64) NOT NULL,
  `api_version` varchar(64) NOT NULL,
  `author` varchar(128) NOT NULL,
  `description` varchar(512) NOT NULL,
  `download_url` varchar(512) NOT NULL,
  `size_bytes` bigint NOT NULL DEFAULT 0,
  `sha256` char(64) NOT NULL,
  `minimum_framework_version` varchar(64) NOT NULL,
  `is_active` tinyint(1) NOT NULL DEFAULT 1,
  `sort_order` int NOT NULL DEFAULT 0,
  `created_at` timestamp NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `updated_at` timestamp NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  PRIMARY KEY (`plugin_id`),
  UNIQUE KEY `uq_launcher_umbra_plugin` (`repository_id`, `plugin_key`, `version`),
  CONSTRAINT `fk_launcher_umbra_plugins_repository`
    FOREIGN KEY (`repository_id`) REFERENCES `launcher_umbra_plugin_repositories` (`repository_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS `launcher_umbra_plugin_blocks` (
  `block_id` int unsigned NOT NULL AUTO_INCREMENT,
  `plugin_key` varchar(128) NOT NULL,
  `repository_url` varchar(255) NOT NULL,
  `version` varchar(64) DEFAULT NULL,
  `reason` varchar(255) NOT NULL,
  `is_active` tinyint(1) NOT NULL DEFAULT 1,
  `created_at` timestamp NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (`block_id`),
  KEY `idx_launcher_umbra_plugin_blocks_active` (`is_active`, `plugin_key`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

INSERT IGNORE INTO `launcher_config` (
  `config_key`, `service_version`, `server_name`, `server_status_url`, `news_url`, `patch_manifest_url`,
  `runtime_catalog_url`, `login_url`, `account_create_url`, `client_login_url`, `patch_base_url`,
  `target_boot_version`, `target_game_version`, `client_plugin_framework_catalog_url`,
  `plugin_blocklist_url`, `is_active`
)
SELECT
  'local',
  CAST(COALESCE((SELECT config_value FROM launcher_config_v13 WHERE config_key='service_version'), '1') AS UNSIGNED),
  CASE COALESCE((SELECT config_value FROM launcher_config_v13 WHERE config_key='server_name'), '')
    WHEN '' THEN 'AetherXIV 2 Local'
    WHEN 'AetherXIV Core v1.3' THEN 'AetherXIV 2 Local'
    WHEN 'AetherXIV Core 2.0 Local' THEN 'AetherXIV 2 Local'
    ELSE (SELECT config_value FROM launcher_config_v13 WHERE config_key='server_name')
  END,
  'status', 'news', 'patch-manifest',
  COALESCE((SELECT config_value FROM launcher_config_v13 WHERE config_key='runtime_catalog_url'), 'runtime-catalog'),
  COALESCE((SELECT config_value FROM launcher_config_v13 WHERE config_key='login_url'), 'login'),
  COALESCE((SELECT config_value FROM launcher_config_v13 WHERE config_key='account_create_url'), 'create-account'),
  COALESCE((SELECT config_value FROM launcher_config_v13 WHERE config_key='client_login_url'), '../login/index.php'),
  COALESCE((SELECT config_value FROM launcher_config_v13 WHERE config_key='patch_base_url'), ''),
  COALESCE((SELECT config_value FROM launcher_config_v13 WHERE config_key='target_boot_version'), '2010.09.18.0000'),
  COALESCE((SELECT config_value FROM launcher_config_v13 WHERE config_key='target_game_version'), '2012.09.19.0001'),
  COALESCE((SELECT config_value FROM launcher_config_v13 WHERE config_key='client_plugin_framework_catalog_url'), 'umbra/framework-catalog'),
  COALESCE((SELECT config_value FROM launcher_config_v13 WHERE config_key='plugin_blocklist_url'), 'umbra/plugin-blocklist'),
  1
WHERE @has_modern_contract=0;

INSERT IGNORE INTO `launcher_config_plugin_catalogs` (`config_key`, `catalog_url`, `sort_order`)
SELECT 'local', 'umbra/plugin-catalog', 0 WHERE @has_modern_contract=0;

INSERT IGNORE INTO `launcher_status` (`status_key`, `state`, `message`)
SELECT
  'default',
  COALESCE((SELECT config_value FROM launcher_config_v13 WHERE config_key='server_state'), 'online'),
  COALESCE((SELECT NULLIF(config_value, '') FROM launcher_config_v13 WHERE config_key='server_message'), 'AetherXIV local launcher service is online.')
WHERE @has_modern_contract=0;

INSERT IGNORE INTO `launcher_news` (
  `news_id`, `title`, `summary`, `body`, `banner_url`, `link_url`, `published_at`,
  `is_active`, `sort_order`, `title_color`, `summary_color`, `body_color`
)
SELECT `id`, `title`, `summary`, `body`, `banner_url`, `link_url`, `published_at`,
       `is_published`, `sort_order`, '#F2F4FA', '#D6DCE3', '#AEB7C2'
FROM `launcher_news_v13`
WHERE @has_modern_contract=0;

INSERT IGNORE INTO `launcher_patch_files` (
  `patch_file_id`, `target_boot_version`, `target_game_version`, `relative_path`,
  `size_bytes`, `crc32`, `sha256`, `is_active`, `sort_order`
)
SELECT `id`,
       COALESCE((SELECT config_value FROM launcher_config_v13 WHERE config_key='target_boot_version'), '2010.09.18.0000'),
       COALESCE((SELECT config_value FROM launcher_config_v13 WHERE config_key='target_game_version'), '2012.09.19.0001'),
       `relative_path`, `size_bytes`, `crc32`, `sha256`, `is_active`, `sort_order`
FROM `launcher_patch_files_v13`
WHERE @has_modern_contract=0;

INSERT IGNORE INTO `launcher_runtime_artifacts` (
  `artifact_id`, `name`, `version`, `platform_rid`, `runtime_kind`, `archive_url`, `archive_format`,
  `size_bytes`, `sha256`, `executable_relative_path`, `prefix_arch`, `environment_json`,
  `is_default`, `is_active`, `sort_order`, `created_at`, `updated_at`
)
SELECT `id`, `name`, `version`, `platform_rid`, `runtime_kind`, `archive_url`, `archive_format`,
       `size_bytes`, `sha256`, `executable_relative_path`, `prefix_arch`, COALESCE(`environment_json`, JSON_OBJECT()),
       `is_default`, `is_active`, `sort_order`, `created_at`, `updated_at`
FROM `launcher_runtime_artifacts_v13`
WHERE @has_modern_contract=0;

INSERT IGNORE INTO `launcher_umbra_framework_artifacts` (
  `artifact_id`, `name`, `version`, `api_version`, `platform_rid`, `archive_url`, `archive_format`,
  `size_bytes`, `sha256`, `bootstrap_relative_path`, `framework_relative_path`,
  `supported_game_sha256_json`, `is_default`, `is_active`, `sort_order`, `created_at`, `updated_at`
)
SELECT `id`, `name`, `version`, `api_version`, `platform_rid`, `archive_url`, `archive_format`,
       `size_bytes`, `sha256`, `bootstrap_relative_path`, `framework_relative_path`,
       IF(JSON_VALID(`supported_game_sha256`), `supported_game_sha256`, JSON_ARRAY()),
       `is_default`, `is_active`, `sort_order`, `created_at`, `updated_at`
FROM `launcher_umbra_framework_artifacts_v13`
WHERE @has_modern_contract=0;

INSERT IGNORE INTO `launcher_presentation` (`presentation_key`, `reel_text_enabled`)
VALUES ('default', 0);
