-- Give the Gridania opening companions real battle NPC action packages.
-- The director controls when they are released into combat; these lists define
-- what the existing BattleNpcController may choose once they are engaged.
--
-- These IDs intentionally reference rows that exist in server_battle_commands.
-- Historical 1.x names such as Phantom Dart, Scourge, Dia, Light Strike, and
-- Jarring Strike should be added only after their full command rows are mapped.

CREATE TABLE IF NOT EXISTS `server_battlenpc_skill_list` (
  `skillListId` int(10) unsigned NOT NULL DEFAULT '0',
  `skillId` int(10) unsigned NOT NULL DEFAULT '0',
  PRIMARY KEY (`skillListId`, `skillId`)
) ENGINE=InnoDB AUTO_INCREMENT=1 DEFAULT CHARSET=utf8;

CREATE TABLE IF NOT EXISTS `server_battlenpc_spell_list` (
  `spellListId` int(10) unsigned NOT NULL DEFAULT '0',
  `spellId` int(10) unsigned NOT NULL DEFAULT '0',
  PRIMARY KEY (`spellListId`, `spellId`)
) ENGINE=InnoDB AUTO_INCREMENT=1 DEFAULT CHARSET=utf8;

INSERT IGNORE INTO `server_battlenpc_skill_list` (`skillListId`, `skillId`) VALUES
  (30010, 27110),
  (30010, 27111),
  (30010, 27114);

INSERT IGNORE INTO `server_battlenpc_spell_list` (`spellListId`, `spellId`) VALUES
  (30011, 27308),
  (30011, 27310),
  (30011, 27313);

UPDATE `server_battlenpc_pools`
   SET `skillListId` = 30010,
       `spellListId` = 0
 WHERE `poolId` = 3
   AND `name` = 'yda';

UPDATE `server_battlenpc_pools`
   SET `spellListId` = 30011,
       `skillListId` = 0
 WHERE `poolId` = 4
   AND `name` = 'papalymo';
