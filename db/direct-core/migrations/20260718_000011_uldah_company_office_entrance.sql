-- Restore the Ul'dah MarketEntrance trigger used to enter the Hall of Flames.
-- The prior row defined an inert trigger box with zero bg-object/layout IDs.
-- MarketEntrance.lua records the 1.x Ul'dah values; official Gridania frame 93
-- independently confirms their positions in opcode 0x0175.

UPDATE `gamedata_actor_class`
SET `classPath`='/Chara/Npc/Object/MarketEntrance',
    `displayNameId`=0,
    `propertyFlags`=1,
    `eventConditions`='{"noticeEventConditions":[{"unknown1":1,"unknown2":0,"conditionName":"noticeEvent"}],"emoteEventConditions":[],"pushWithBoxEventConditions":[{"bgObj":4143,"layout":421,"conditionName":"in","reactName":"dtwi","silent":false,"outwards":false}]}'
WHERE `id`=1090265;

INSERT INTO `server_npc_spawn_evidence`
(`evidenceId`,`service`,`spawnId`,`actorClassId`,`zoneId`,`privateAreaName`,`positionX`,`positionY`,`positionZ`,`rotation`,`classPath`,`appearanceId`,`evidenceSource`,`evidenceReference`,`clientBuild`,`confidenceStatus`)
VALUES
('company-office-entrance-immortal-flames','grand-company-office-entrance',104,1090265,175,'',-235,189,50.5,0,'/Chara/Npc/Object/MarketEntrance',1090265,'client-actor-catalog-reviewed-seed-and-official-trigger-packet','server_spawn_locations:104; MarketEntrance.lua Ul''dah trigger metadata; ffxiv_traces/moving_around_gridania.pcapng frame:93 validates 0x0175 field semantics','2012.09.19.0001','RepoConfirmed')
ON DUPLICATE KEY UPDATE
`service`=VALUES(`service`),`spawnId`=VALUES(`spawnId`),`actorClassId`=VALUES(`actorClassId`),
`zoneId`=VALUES(`zoneId`),`privateAreaName`=VALUES(`privateAreaName`),
`positionX`=VALUES(`positionX`),`positionY`=VALUES(`positionY`),`positionZ`=VALUES(`positionZ`),
`rotation`=VALUES(`rotation`),`classPath`=VALUES(`classPath`),`appearanceId`=VALUES(`appearanceId`),
`evidenceSource`=VALUES(`evidenceSource`),`evidenceReference`=VALUES(`evidenceReference`),
`clientBuild`=VALUES(`clientBuild`),`confidenceStatus`=VALUES(`confidenceStatus`);

INSERT INTO `server_npc_spawn_evidence_catalog`
(`catalogId`,`version`,`contentHashSha256`,`clientBuild`,`recordCount`)
VALUES ('zone-service-npcs-1.23b','2026.07.18.3','3cba7e19a8af1ce04941a7c42ab81073a99c9370f8a3f27ccd0921051ad804e1','2012.09.19.0001',23)
ON DUPLICATE KEY UPDATE
`version`=VALUES(`version`),`contentHashSha256`=VALUES(`contentHashSha256`),
`clientBuild`=VALUES(`clientBuild`),`recordCount`=VALUES(`recordCount`);
