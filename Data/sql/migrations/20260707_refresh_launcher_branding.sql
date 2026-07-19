-- Refresh stale Meteor branding left in launcher rows on upgraded v1.2 databases.

CREATE TABLE IF NOT EXISTS `launcher_config` (
  `config_key` varchar(64) NOT NULL,
  `config_value` text NOT NULL,
  `updated_at` timestamp NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  PRIMARY KEY (`config_key`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8;

CREATE TABLE IF NOT EXISTS `launcher_news` (
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

INSERT INTO `launcher_config` (`config_key`, `config_value`) VALUES
  ('server_name', 'AetherXIV Core v1.3')
ON DUPLICATE KEY UPDATE
  `config_value` = VALUES(`config_value`);

INSERT IGNORE INTO `launcher_config` (`config_key`, `config_value`) VALUES
  ('server_state', 'offline'),
  ('server_message', 'Launcher service is installed. Game services are not reporting status yet.');

UPDATE `launcher_news`
SET
  `summary` = REPLACE(REPLACE(`summary`, 'MeteorXIV', 'AetherXIV'), 'Meteor database', 'AetherXIV database'),
  `body` = REPLACE(REPLACE(`body`, 'MeteorXIV', 'AetherXIV'), 'Meteor database', 'AetherXIV database')
WHERE `summary` LIKE '%Meteor%' OR `body` LIKE '%Meteor%';

INSERT INTO `launcher_news` (`title`, `summary`, `body`, `published_at`, `sort_order`)
SELECT
  'Echo Gate service installed',
  'Launcher news is now served from the AetherXIV database.',
  'Use launcher_news rows to publish updates for testers.',
  UTC_TIMESTAMP(),
  0
WHERE NOT EXISTS (
  SELECT 1 FROM `launcher_news` WHERE `title` = 'Echo Gate service installed'
);
