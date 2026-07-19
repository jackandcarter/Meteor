-- Correct the Hall of Flames exit to the Ul'dah background-object pair that
-- the legacy actor-class row and MarketEntrance script preserve. Direct client
-- testing disproved the Gridania pair applied by migration 000012: Ian Five
-- reached the door collision plane without the client emitting an event.

UPDATE `gamedata_actor_class`
SET `classPath`='/Chara/Npc/Object/MarketEntrance',
    `displayNameId`=0,
    `propertyFlags`=1,
    `eventConditions`='{"noticeEventConditions":[{"unknown1":0,"unknown2":1,"conditionName":"noticeEvent"}],"pushWithBoxEventConditions":[{"bgObj":4143,"layout":421,"conditionName":"in","reactName":"dtwi","silent":false,"outwards":false}]}'
WHERE `id`=1090264;

UPDATE `server_npc_spawn_evidence`
SET `evidenceSource`='reviewed-1x-layout-legacy-class-runtime-observation',
    `evidenceReference`='The-Primal-Launcher@49169ae25b034ed65bcec9e4abdc68a6fb229f52:PrimalLauncher/Resources/xml/zones/xE9/npc.xml assigns actor class 1090264 at 160/0/-142.76; legacy gamedata_actor_class:1090264 preserves bgObj 4143; MarketEntrance.lua preserves the Ul''dah 4143/421/in/dtwi pair; 2026-07-19 direct-core runtime reached 159.57256/0/-144.57875 without a client event when the Gridania 3322/321 pair was used',
    `confidenceStatus`='RepoConfirmed'
WHERE `evidenceId`='company-office-exit-immortal-flames';

INSERT INTO `server_npc_spawn_evidence_catalog`
(`catalogId`,`version`,`contentHashSha256`,`clientBuild`,`recordCount`)
VALUES ('zone-service-npcs-1.23b','2026.07.18.5','3f75408e7d84d9d8c132fbca80eb78db7839e924ddf2f25efe80f8a74f7c2381','2012.09.19.0001',23)
ON DUPLICATE KEY UPDATE
`version`=VALUES(`version`),`contentHashSha256`=VALUES(`contentHashSha256`),
`clientBuild`=VALUES(`clientBuild`),`recordCount`=VALUES(`recordCount`);
