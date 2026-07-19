-- Echo Gate launcher service tables.
-- These tables back launcher config, status, news, and patch manifest endpoints.

DROP TABLE IF EXISTS `launcher_config`;
CREATE TABLE `launcher_config` (
  `config_key` varchar(64) NOT NULL,
  `config_value` text NOT NULL,
  `updated_at` timestamp NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  PRIMARY KEY (`config_key`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8;

INSERT INTO `launcher_config` (`config_key`, `config_value`) VALUES
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

DROP TABLE IF EXISTS `launcher_news`;
CREATE TABLE `launcher_news` (
  `id` int(11) unsigned NOT NULL AUTO_INCREMENT,
  `title` varchar(160) NOT NULL,
  `summary` varchar(500) NOT NULL,
  `body` text NULL,
  `banner_url` varchar(500) NULL,
  `link_url` varchar(500) NULL,
  `published_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `is_published` tinyint(1) NOT NULL DEFAULT 1,
  `sort_order` int(11) NOT NULL DEFAULT 0,
  PRIMARY KEY (`id`),
  KEY `idx_launcher_news_published` (`is_published`, `published_at`, `sort_order`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8;

INSERT INTO `launcher_news` (`title`, `summary`, `body`, `published_at`, `sort_order`) VALUES
  ('Echo Gate service installed', 'Launcher news is now served from the AetherXIV database.', 'Use launcher_news rows to publish updates for testers.', UTC_TIMESTAMP(), 0);

DROP TABLE IF EXISTS `launcher_patch_files`;
CREATE TABLE `launcher_patch_files` (
  `id` int(11) unsigned NOT NULL AUTO_INCREMENT,
  `repository_id` varchar(16) NOT NULL,
  `relative_path` varchar(255) NOT NULL,
  `size_bytes` bigint(20) NOT NULL,
  `crc32` char(8) NOT NULL,
  `sha256` char(64) NULL,
  `sort_order` int(11) NOT NULL DEFAULT 0,
  `is_active` tinyint(1) NOT NULL DEFAULT 1,
  PRIMARY KEY (`id`),
  UNIQUE KEY `uq_launcher_patch_files_relative_path` (`relative_path`),
  KEY `idx_launcher_patch_files_active` (`is_active`, `sort_order`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8;

INSERT INTO `launcher_patch_files` (`repository_id`, `relative_path`, `size_bytes`, `crc32`, `sort_order`, `is_active`) VALUES
  ('2d2a390f', 'ffxiv/2d2a390f/patch/D2010.09.18.0000.patch', 5571687, '47DDE5ED', 0, 1),
  ('48eca647', 'ffxiv/48eca647/patch/D2010.09.19.0000.patch', 444398866, 'D55C7ACD', 1, 1),
  ('48eca647', 'ffxiv/48eca647/patch/D2010.09.23.0000.patch', 6907277, 'CA135D55', 2, 1),
  ('48eca647', 'ffxiv/48eca647/patch/D2010.09.28.0000.patch', 18803280, 'B19B32FE', 3, 1),
  ('48eca647', 'ffxiv/48eca647/patch/D2010.10.07.0001.patch', 19226330, 'D6118CEE', 4, 1),
  ('48eca647', 'ffxiv/48eca647/patch/D2010.10.14.0000.patch', 19464329, '34BF6A99', 5, 1),
  ('48eca647', 'ffxiv/48eca647/patch/D2010.10.22.0000.patch', 19778252, '2543DB5C', 6, 1),
  ('48eca647', 'ffxiv/48eca647/patch/D2010.10.26.0000.patch', 19778391, '20F94876', 7, 1),
  ('48eca647', 'ffxiv/48eca647/patch/D2010.11.25.0002.patch', 250718651, '5FBB5B24', 8, 1),
  ('48eca647', 'ffxiv/48eca647/patch/D2010.11.30.0000.patch', 6921623, 'A5479111', 9, 1),
  ('48eca647', 'ffxiv/48eca647/patch/D2010.12.06.0000.patch', 7158904, 'CAD6BC31', 10, 1),
  ('48eca647', 'ffxiv/48eca647/patch/D2010.12.13.0000.patch', 263311481, 'E51EFC06', 11, 1),
  ('48eca647', 'ffxiv/48eca647/patch/D2010.12.21.0000.patch', 7521358, '93EE1510', 12, 1),
  ('48eca647', 'ffxiv/48eca647/patch/D2011.01.18.0000.patch', 9954265, '059E8900', 13, 1),
  ('48eca647', 'ffxiv/48eca647/patch/D2011.02.01.0000.patch', 11632816, '9EE60B39', 14, 1),
  ('48eca647', 'ffxiv/48eca647/patch/D2011.02.10.0000.patch', 11714096, '0ADE7243', 15, 1),
  ('48eca647', 'ffxiv/48eca647/patch/D2011.03.01.0000.patch', 77464101, '7818B5BF', 16, 1),
  ('48eca647', 'ffxiv/48eca647/patch/D2011.03.24.0000.patch', 108923937, 'F21852AD', 17, 1),
  ('48eca647', 'ffxiv/48eca647/patch/D2011.03.30.0000.patch', 109010880, '84CB2682', 18, 1),
  ('48eca647', 'ffxiv/48eca647/patch/D2011.04.13.0000.patch', 341603850, 'FF6C3DB0', 19, 1),
  ('48eca647', 'ffxiv/48eca647/patch/D2011.04.21.0000.patch', 343579198, '57F4041C', 20, 1),
  ('48eca647', 'ffxiv/48eca647/patch/D2011.05.19.0000.patch', 344239925, 'B16FF18C', 21, 1),
  ('48eca647', 'ffxiv/48eca647/patch/D2011.06.10.0000.patch', 344334860, 'B1CAA88B', 22, 1),
  ('48eca647', 'ffxiv/48eca647/patch/D2011.07.20.0000.patch', 584926805, '2EA149A9', 23, 1),
  ('48eca647', 'ffxiv/48eca647/patch/D2011.07.26.0000.patch', 7649141, '5670BA07', 24, 1),
  ('48eca647', 'ffxiv/48eca647/patch/D2011.08.05.0000.patch', 152064532, '0D9E9FD8', 25, 1),
  ('48eca647', 'ffxiv/48eca647/patch/D2011.08.09.0000.patch', 8573687, '9B54551A', 26, 1),
  ('48eca647', 'ffxiv/48eca647/patch/D2011.08.16.0000.patch', 6118907, '75231C57', 27, 1),
  ('48eca647', 'ffxiv/48eca647/patch/D2011.10.04.0000.patch', 677633296, '95C15318', 28, 1),
  ('48eca647', 'ffxiv/48eca647/patch/D2011.10.12.0001.patch', 28941655, 'B37993E3', 29, 1),
  ('48eca647', 'ffxiv/48eca647/patch/D2011.10.27.0000.patch', 29179764, '977480DC', 30, 1),
  ('48eca647', 'ffxiv/48eca647/patch/D2011.12.14.0000.patch', 374617428, 'C6FE8FED', 31, 1),
  ('48eca647', 'ffxiv/48eca647/patch/D2011.12.23.0000.patch', 22363713, '93137C93', 32, 1),
  ('48eca647', 'ffxiv/48eca647/patch/D2012.01.18.0000.patch', 48998794, '9E55EC7E', 33, 1),
  ('48eca647', 'ffxiv/48eca647/patch/D2012.01.24.0000.patch', 49126606, '3008D942', 34, 1),
  ('48eca647', 'ffxiv/48eca647/patch/D2012.01.31.0000.patch', 49536396, '60FDBD0B', 35, 1),
  ('48eca647', 'ffxiv/48eca647/patch/D2012.03.07.0000.patch', 320630782, '885AD768', 36, 1),
  ('48eca647', 'ffxiv/48eca647/patch/D2012.03.09.0000.patch', 8312819, 'C0040D8C', 37, 1),
  ('48eca647', 'ffxiv/48eca647/patch/D2012.03.22.0000.patch', 22027738, 'EABC501B', 38, 1),
  ('48eca647', 'ffxiv/48eca647/patch/D2012.03.29.0000.patch', 8322920, '63811C35', 39, 1),
  ('48eca647', 'ffxiv/48eca647/patch/D2012.04.04.0000.patch', 8678570, 'F6E43EEC', 40, 1),
  ('48eca647', 'ffxiv/48eca647/patch/D2012.04.23.0001.patch', 289511791, '6C3C0201', 41, 1),
  ('48eca647', 'ffxiv/48eca647/patch/D2012.05.08.0000.patch', 27266546, 'B6AABF18', 42, 1),
  ('48eca647', 'ffxiv/48eca647/patch/D2012.05.15.0000.patch', 27416023, '2D428126', 43, 1),
  ('48eca647', 'ffxiv/48eca647/patch/D2012.05.22.0000.patch', 27742726, '9163549D', 44, 1),
  ('48eca647', 'ffxiv/48eca647/patch/D2012.06.06.0000.patch', 129984024, '21DF7238', 45, 1),
  ('48eca647', 'ffxiv/48eca647/patch/D2012.06.19.0000.patch', 133434217, '8280988A', 46, 1),
  ('48eca647', 'ffxiv/48eca647/patch/D2012.06.26.0000.patch', 133581048, '4CF33FC8', 47, 1),
  ('48eca647', 'ffxiv/48eca647/patch/D2012.07.21.0000.patch', 253224781, 'A8A42A32', 48, 1),
  ('48eca647', 'ffxiv/48eca647/patch/D2012.08.10.0000.patch', 42851112, 'D8ED4CE3', 49, 1),
  ('48eca647', 'ffxiv/48eca647/patch/D2012.09.06.0000.patch', 20566711, '4235DF72', 50, 1),
  ('48eca647', 'ffxiv/48eca647/patch/D2012.09.19.0001.patch', 20874726, '8A775526', 51, 1);

DROP TABLE IF EXISTS `launcher_runtime_artifacts`;
CREATE TABLE `launcher_runtime_artifacts` (
  `id` int(11) unsigned NOT NULL AUTO_INCREMENT,
  `name` varchar(120) NOT NULL,
  `version` varchar(64) NOT NULL,
  `platform_rid` varchar(32) NOT NULL,
  `runtime_kind` varchar(32) NOT NULL,
  `archive_url` varchar(500) NOT NULL,
  `archive_format` varchar(16) NOT NULL DEFAULT 'zip',
  `size_bytes` bigint(20) NOT NULL,
  `sha256` char(64) NOT NULL,
  `executable_relative_path` varchar(255) NOT NULL,
  `prefix_arch` varchar(32) NOT NULL DEFAULT 'win64',
  `environment_json` text NULL,
  `is_default` tinyint(1) NOT NULL DEFAULT 0,
  `is_active` tinyint(1) NOT NULL DEFAULT 1,
  `sort_order` int(11) NOT NULL DEFAULT 0,
  `created_at` timestamp NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `updated_at` timestamp NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  PRIMARY KEY (`id`),
  KEY `idx_launcher_runtime_artifacts_platform` (`platform_rid`, `is_active`, `is_default`, `sort_order`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8;

DROP TABLE IF EXISTS `launcher_umbra_framework_artifacts`;
CREATE TABLE `launcher_umbra_framework_artifacts` (
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

DROP TABLE IF EXISTS `launcher_umbra_plugin_repositories`;
CREATE TABLE `launcher_umbra_plugin_repositories` (
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

DROP TABLE IF EXISTS `launcher_umbra_plugin_releases`;
CREATE TABLE `launcher_umbra_plugin_releases` (
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
  KEY `idx_launcher_umbra_plugin_releases_active` (`repository_id`, `is_active`, `sort_order`),
  CONSTRAINT `fk_launcher_umbra_plugin_releases_repository`
    FOREIGN KEY (`repository_id`) REFERENCES `launcher_umbra_plugin_repositories` (`id`)
    ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8;

DROP TABLE IF EXISTS `launcher_umbra_plugin_blocks`;
CREATE TABLE `launcher_umbra_plugin_blocks` (
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
