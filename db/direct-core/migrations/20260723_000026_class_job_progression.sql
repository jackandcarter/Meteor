-- Persist the active 1.x job independently from the equipped base class.
ALTER TABLE `characters`
  ADD COLUMN IF NOT EXISTS `currentJob` tinyint(3) unsigned NOT NULL DEFAULT '0'
  AFTER `currentTitle`;
