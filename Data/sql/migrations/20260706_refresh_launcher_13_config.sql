-- Refresh launcher service config for databases upgraded from Meteor/Echo Gate v1.2.
-- The original v1.3 migration used INSERT IGNORE so existing v1.2 config rows
-- could remain visible in the launcher even after the Umbra tables were added.

INSERT INTO `launcher_config` (`config_key`, `config_value`) VALUES
  ('service_version', '1'),
  ('launcher_version', '1.3'),
  ('server_name', 'AetherXIV Core v1.3'),
  ('login_url', 'login'),
  ('account_create_url', 'create-account'),
  ('client_login_url', '../login/index.php'),
  ('runtime_catalog_url', 'runtime-catalog'),
  ('client_plugin_framework_catalog_url', 'umbra/framework-catalog'),
  ('plugin_blocklist_url', 'umbra/plugin-blocklist'),
  ('target_boot_version', '2010.09.18.0000'),
  ('target_game_version', '2012.09.19.0001')
ON DUPLICATE KEY UPDATE
  `config_value` = VALUES(`config_value`);
