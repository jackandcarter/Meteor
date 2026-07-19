-- Repair direct-core installations created before the legacy class-allocation
-- table was included in the canonical baseline. Fresh baselines already contain
-- the same table; CREATE IF NOT EXISTS keeps this migration idempotent.

CREATE TABLE IF NOT EXISTS `characters_class_attributes` (
  `characterId` int(10) unsigned NOT NULL,
  `classId` tinyint(3) unsigned NOT NULL,
  `pointsRemaining` smallint(6) NOT NULL DEFAULT '0',
  `strSpent` smallint(6) NOT NULL DEFAULT '0',
  `vitSpent` smallint(6) NOT NULL DEFAULT '0',
  `dexSpent` smallint(6) NOT NULL DEFAULT '0',
  `intSpent` smallint(6) NOT NULL DEFAULT '0',
  `mndSpent` smallint(6) NOT NULL DEFAULT '0',
  `pieSpent` smallint(6) NOT NULL DEFAULT '0',
  PRIMARY KEY (`characterId`,`classId`)
) ENGINE=InnoDB DEFAULT CHARSET=latin1;
