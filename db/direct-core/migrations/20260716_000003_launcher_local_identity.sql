-- Normalize only obsolete built-in local identities. Public hosts keep custom names.
UPDATE `launcher_config`
SET `server_name` = 'AetherXIV 2 Local',
    `updated_at` = CURRENT_TIMESTAMP
WHERE `config_key` = 'local'
  AND `server_name` IN (
    'AetherXIV Core v1.3',
    'AetherXIV Core 2.0 Local'
  );
