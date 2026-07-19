-- Provisional in-game spawn pin captures.
-- Rows in this table are audit/workbench data only; they do not spawn actors.
-- Promote reviewed pins into server_battlenpc_spawn_locations through a later migration.

CREATE TABLE IF NOT EXISTS server_battlenpc_spawn_audit_pins (
    pinId INT UNSIGNED NOT NULL AUTO_INCREMENT,
    enemyName VARCHAR(64) NOT NULL,
    sourceNote VARCHAR(255) NOT NULL DEFAULT '',
    zoneId INT UNSIGNED NOT NULL,
    positionX FLOAT NOT NULL,
    positionY FLOAT NOT NULL,
    positionZ FLOAT NOT NULL,
    rotation FLOAT NOT NULL,
    createdByCharacterId INT UNSIGNED DEFAULT NULL,
    createdByCharacterName VARCHAR(64) NOT NULL DEFAULT '',
    createdAt TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    isPromoted TINYINT(1) NOT NULL DEFAULT 0,
    promotedAt TIMESTAMP NULL DEFAULT NULL,
    promotionMigration VARCHAR(128) DEFAULT NULL,
    promotionNote VARCHAR(255) DEFAULT NULL,
    PRIMARY KEY (pinId),
    KEY idx_battlenpc_spawn_audit_zone_enemy (zoneId, enemyName),
    KEY idx_battlenpc_spawn_audit_promoted (isPromoted, createdAt)
) ENGINE=InnoDB DEFAULT CHARSET=utf8;
