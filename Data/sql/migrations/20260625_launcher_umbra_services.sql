-- Non-destructive launcher service migration for Aether Umbra.
-- Safe for existing VPS/dev databases: it creates missing launcher/Umbra tables
-- and adds columns needed by the v1.3 Umbra catalog flow without dropping data.

CREATE TABLE IF NOT EXISTS `launcher_config` (
  `config_key` varchar(64) NOT NULL,
  `config_value` text NOT NULL,
  `updated_at` timestamp NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  PRIMARY KEY (`config_key`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8;

INSERT IGNORE INTO `launcher_config` (`config_key`, `config_value`) VALUES
  ('service_version', '1'),
  ('launcher_version', '1.3'),
  ('server_name', 'AetherXIV Core v1.3'),
  ('server_state', 'offline'),
  ('server_message', 'Launcher service is installed. Game services are not reporting status yet.'),
  ('patch_base_url', ''),
  ('login_url', 'login'),
  ('account_create_url', 'create-account'),
  ('client_login_url', '../login/index.php'),
  ('runtime_catalog_url', 'runtime-catalog'),
  ('client_plugin_framework_catalog_url', 'umbra/framework-catalog'),
  ('plugin_catalog_urls', ''),
  ('plugin_blocklist_url', 'umbra/plugin-blocklist'),
  ('target_boot_version', '2010.09.18.0000'),
  ('target_game_version', '2012.09.19.0001');

CREATE TABLE IF NOT EXISTS `launcher_umbra_framework_artifacts` (
  `id` int(11) unsigned NOT NULL AUTO_INCREMENT,
  `name` varchar(120) NOT NULL,
  `version` varchar(64) NOT NULL,
  `api_version` varchar(32) NOT NULL DEFAULT '1.0',
  `platform_rid` varchar(32) NOT NULL DEFAULT 'win-x86',
  `archive_url` varchar(500) NOT NULL,
  `archive_format` varchar(16) NOT NULL DEFAULT 'zip',
  `size_bytes` bigint(20) NOT NULL,
  `sha256` char(64) NOT NULL,
  `bootstrap_relative_path` varchar(255) NOT NULL DEFAULT 'Aether.Umbra.Bootstrap.x86.dll',
  `framework_relative_path` varchar(255) NOT NULL DEFAULT 'Managed/Aether.Umbra.Framework.dll',
  `supported_game_sha256` text NULL,
  `is_default` tinyint(1) NOT NULL DEFAULT 0,
  `is_active` tinyint(1) NOT NULL DEFAULT 1,
  `sort_order` int(11) NOT NULL DEFAULT 0,
  `created_at` timestamp NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `updated_at` timestamp NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  PRIMARY KEY (`id`),
  KEY `idx_launcher_umbra_framework_platform` (`platform_rid`, `is_active`, `is_default`, `sort_order`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8;

CREATE TABLE IF NOT EXISTS `launcher_umbra_plugin_repositories` (
  `id` int(11) unsigned NOT NULL AUTO_INCREMENT,
  `repository_key` varchar(64) NOT NULL,
  `name` varchar(120) NOT NULL,
  `description` varchar(500) NULL,
  `repository_url` varchar(500) NOT NULL,
  `repository_kind` varchar(32) NOT NULL DEFAULT 'supported',
  `is_supported` tinyint(1) NOT NULL DEFAULT 1,
  `is_active` tinyint(1) NOT NULL DEFAULT 1,
  `sort_order` int(11) NOT NULL DEFAULT 0,
  `last_error` varchar(1000) NULL,
  `created_at` timestamp NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `updated_at` timestamp NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  PRIMARY KEY (`id`),
  UNIQUE KEY `uq_launcher_umbra_plugin_repositories_key` (`repository_key`),
  UNIQUE KEY `uq_launcher_umbra_plugin_repositories_url` (`repository_url`),
  KEY `idx_launcher_umbra_plugin_repositories_active` (`is_supported`, `is_active`, `sort_order`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8;

ALTER TABLE `launcher_umbra_plugin_repositories`
  ADD COLUMN IF NOT EXISTS `repository_key` varchar(64) NOT NULL DEFAULT '' AFTER `id`,
  ADD COLUMN IF NOT EXISTS `name` varchar(120) NOT NULL DEFAULT '' AFTER `repository_key`,
  ADD COLUMN IF NOT EXISTS `description` varchar(500) NULL AFTER `name`,
  ADD COLUMN IF NOT EXISTS `repository_url` varchar(500) NOT NULL DEFAULT '' AFTER `description`,
  ADD COLUMN IF NOT EXISTS `repository_kind` varchar(32) NOT NULL DEFAULT 'supported' AFTER `repository_url`,
  ADD COLUMN IF NOT EXISTS `is_supported` tinyint(1) NOT NULL DEFAULT 1 AFTER `repository_kind`,
  ADD COLUMN IF NOT EXISTS `is_active` tinyint(1) NOT NULL DEFAULT 1 AFTER `is_supported`,
  ADD COLUMN IF NOT EXISTS `sort_order` int(11) NOT NULL DEFAULT 0 AFTER `is_active`,
  ADD COLUMN IF NOT EXISTS `last_error` varchar(1000) NULL AFTER `sort_order`,
  ADD COLUMN IF NOT EXISTS `created_at` timestamp NOT NULL DEFAULT CURRENT_TIMESTAMP AFTER `last_error`,
  ADD COLUMN IF NOT EXISTS `updated_at` timestamp NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP AFTER `created_at`;

CREATE TABLE IF NOT EXISTS `launcher_umbra_plugin_releases` (
  `id` int(11) unsigned NOT NULL AUTO_INCREMENT,
  `repository_id` int(11) unsigned NOT NULL,
  `plugin_key` varchar(120) NOT NULL,
  `name` varchar(160) NOT NULL,
  `version` varchar(64) NOT NULL,
  `api_version` varchar(32) NOT NULL DEFAULT '1.0',
  `author` varchar(160) NOT NULL DEFAULT '',
  `description` varchar(1000) NOT NULL DEFAULT '',
  `download_url` varchar(500) NOT NULL,
  `size_bytes` bigint(20) NOT NULL,
  `sha256` char(64) NOT NULL,
  `minimum_framework_version` varchar(64) NOT NULL DEFAULT '0.1.0',
  `is_active` tinyint(1) NOT NULL DEFAULT 1,
  `sort_order` int(11) NOT NULL DEFAULT 0,
  `created_at` timestamp NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `updated_at` timestamp NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  PRIMARY KEY (`id`),
  UNIQUE KEY `uq_launcher_umbra_plugin_releases_version` (`repository_id`, `plugin_key`, `version`),
  KEY `idx_launcher_umbra_plugin_releases_active` (`repository_id`, `is_active`, `sort_order`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8;

ALTER TABLE `launcher_umbra_plugin_releases`
  ADD COLUMN IF NOT EXISTS `repository_id` int(11) unsigned NOT NULL DEFAULT 0 AFTER `id`,
  ADD COLUMN IF NOT EXISTS `plugin_key` varchar(120) NOT NULL DEFAULT '' AFTER `repository_id`,
  ADD COLUMN IF NOT EXISTS `name` varchar(160) NOT NULL DEFAULT '' AFTER `plugin_key`,
  ADD COLUMN IF NOT EXISTS `version` varchar(64) NOT NULL DEFAULT '' AFTER `name`,
  ADD COLUMN IF NOT EXISTS `api_version` varchar(32) NOT NULL DEFAULT '1.0' AFTER `version`,
  ADD COLUMN IF NOT EXISTS `author` varchar(160) NOT NULL DEFAULT '' AFTER `api_version`,
  ADD COLUMN IF NOT EXISTS `description` varchar(1000) NOT NULL DEFAULT '' AFTER `author`,
  ADD COLUMN IF NOT EXISTS `download_url` varchar(500) NOT NULL DEFAULT '' AFTER `description`,
  ADD COLUMN IF NOT EXISTS `size_bytes` bigint(20) NOT NULL DEFAULT 0 AFTER `download_url`,
  ADD COLUMN IF NOT EXISTS `sha256` char(64) NOT NULL DEFAULT '' AFTER `size_bytes`,
  ADD COLUMN IF NOT EXISTS `minimum_framework_version` varchar(64) NOT NULL DEFAULT '0.1.0' AFTER `sha256`,
  ADD COLUMN IF NOT EXISTS `is_active` tinyint(1) NOT NULL DEFAULT 1 AFTER `minimum_framework_version`,
  ADD COLUMN IF NOT EXISTS `sort_order` int(11) NOT NULL DEFAULT 0 AFTER `is_active`,
  ADD COLUMN IF NOT EXISTS `created_at` timestamp NOT NULL DEFAULT CURRENT_TIMESTAMP AFTER `sort_order`,
  ADD COLUMN IF NOT EXISTS `updated_at` timestamp NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP AFTER `created_at`;

CREATE TABLE IF NOT EXISTS `launcher_umbra_plugin_blocks` (
  `id` int(11) unsigned NOT NULL AUTO_INCREMENT,
  `plugin_key` varchar(120) NOT NULL,
  `repository_url` varchar(500) NULL,
  `version` varchar(64) NULL,
  `reason` varchar(1000) NOT NULL DEFAULT '',
  `is_active` tinyint(1) NOT NULL DEFAULT 1,
  `created_at` timestamp NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `updated_at` timestamp NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  PRIMARY KEY (`id`),
  KEY `idx_launcher_umbra_plugin_blocks_active` (`is_active`, `plugin_key`, `version`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8;
