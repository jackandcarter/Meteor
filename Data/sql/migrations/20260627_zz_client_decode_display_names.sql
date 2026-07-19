-- Local decoded display-name staging table for actor/NPC/enemy restoration diffs.
-- Runtime loaders do not read this table.

CREATE TABLE IF NOT EXISTS client_decoded_display_name_stage (
    id INT UNSIGNED NOT NULL,
    singularName VARCHAR(255) NOT NULL DEFAULT '',
    pluralName VARCHAR(255) NOT NULL DEFAULT '',
    rawCsvLine LONGTEXT,
    importBatchId INT UNSIGNED NOT NULL,
    importedAt TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY (id),
    KEY idx_client_display_name_singular (singularName),
    KEY idx_client_display_name_batch (importBatchId)
) ENGINE=InnoDB DEFAULT CHARSET=utf8;

ALTER TABLE client_decode_import_batches
    ADD COLUMN IF NOT EXISTS displayNamePath VARCHAR(512) NOT NULL DEFAULT '' AFTER actorGraphicPath,
    ADD COLUMN IF NOT EXISTS displayNameRows INT UNSIGNED NOT NULL DEFAULT 0 AFTER actorGraphicRows;
