-- Local workbench notes for visual appearance preview passes.
-- Rows here are evidence/audit data only; they do not spawn actors.

CREATE TABLE IF NOT EXISTS server_battlenpc_appearance_audit (
    appearanceId INT UNSIGNED NOT NULL,
    expectedName VARCHAR(64) NOT NULL DEFAULT '',
    visualStatus VARCHAR(16) NOT NULL DEFAULT 'unsure',
    sourceNote VARCHAR(255) NOT NULL DEFAULT '',
    notes VARCHAR(255) NOT NULL DEFAULT '',
    updatedBy VARCHAR(64) NOT NULL DEFAULT '',
    updatedAt TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    PRIMARY KEY (appearanceId),
    KEY idx_battlenpc_appearance_audit_status (visualStatus, updatedAt),
    KEY idx_battlenpc_appearance_audit_expected (expectedName)
) ENGINE=InnoDB DEFAULT CHARSET=utf8;
