-- Runtime databases do not contain local actor restoration workbench tables.
-- These tables are not read by Lobby, World, Map, Launcher, or AetherXIV Core.
DROP TABLE IF EXISTS `server_battlenpc_appearance_audit`;
DROP TABLE IF EXISTS `server_battlenpc_restoration_evidence`;
DROP TABLE IF EXISTS `client_decoded_display_name_stage`;
DROP TABLE IF EXISTS `client_decoded_actor_graphic_stage`;
DROP TABLE IF EXISTS `client_decoded_actor_class_stage`;
DROP TABLE IF EXISTS `client_decode_import_batches`;
