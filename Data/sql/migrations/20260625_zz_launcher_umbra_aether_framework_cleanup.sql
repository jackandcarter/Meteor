-- Deactivate legacy Meteor Umbra framework catalog rows.
-- AetherXIV v1.3 launchers pass AETHER_UMBRA_* settings to the injected
-- bootstrap, so older Meteor.Umbra payloads should not be selected for launch.

UPDATE launcher_umbra_framework_artifacts
SET is_active = 0
WHERE name LIKE 'Meteor Umbra%'
   OR archive_url LIKE '%meteor-umbra%'
   OR bootstrap_relative_path LIKE '%Meteor.Umbra%'
   OR framework_relative_path LIKE '%Meteor.Umbra%';
