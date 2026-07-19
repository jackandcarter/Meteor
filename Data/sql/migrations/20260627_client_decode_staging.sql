-- Local decoded-client staging tables for restoration diffs.
-- These tables are workbench evidence only; runtime loaders do not read them.

CREATE TABLE IF NOT EXISTS client_decode_import_batches (
    importBatchId INT UNSIGNED NOT NULL AUTO_INCREMENT,
    sourceLabel VARCHAR(128) NOT NULL DEFAULT '',
    actorClassPath VARCHAR(512) NOT NULL DEFAULT '',
    actorGraphicPath VARCHAR(512) NOT NULL DEFAULT '',
    actorClassRows INT UNSIGNED NOT NULL DEFAULT 0,
    actorGraphicRows INT UNSIGNED NOT NULL DEFAULT 0,
    importedAt TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY (importBatchId),
    KEY idx_client_decode_batches_imported (importedAt)
) ENGINE=InnoDB DEFAULT CHARSET=utf8;

CREATE TABLE IF NOT EXISTS client_decoded_actor_class_stage (
    id INT UNSIGNED NOT NULL,
    classPath VARCHAR(255) NOT NULL DEFAULT '',
    displayNameId INT UNSIGNED NOT NULL DEFAULT 4294967295,
    propertyFlags INT UNSIGNED NOT NULL DEFAULT 0,
    rawCsvLine LONGTEXT,
    importBatchId INT UNSIGNED NOT NULL,
    importedAt TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY (id),
    KEY idx_client_actor_class_path (classPath),
    KEY idx_client_actor_class_batch (importBatchId)
) ENGINE=InnoDB DEFAULT CHARSET=utf8;

CREATE TABLE IF NOT EXISTS client_decoded_actor_graphic_stage (
    id INT UNSIGNED NOT NULL,
    base INT UNSIGNED NOT NULL DEFAULT 0,
    size INT UNSIGNED NOT NULL DEFAULT 0,
    rawCsvLine LONGTEXT,
    importBatchId INT UNSIGNED NOT NULL,
    importedAt TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY (id),
    KEY idx_client_actor_graphic_base (base),
    KEY idx_client_actor_graphic_batch (importBatchId)
) ENGINE=InnoDB DEFAULT CHARSET=utf8;
