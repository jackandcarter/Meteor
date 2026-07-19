-- Restore the Hall of Flames reverse threshold from the reviewed xE9 runtime
-- implementation. Unlike the city-side Ul'dah entrance, the office-side
-- MarketEntrance is an actor-centered 0x016F push circle; it is not tied to a
-- background-object trigger box. The dynamic source actor word is resolved by
-- the Map packet builder when useSourceActorId is true.

UPDATE `gamedata_actor_class`
SET `classPath`='/Chara/Npc/Object/MarketEntrance',
    `displayNameId`=0,
    `propertyFlags`=1,
    `eventConditions`='{"noticeEventConditions":[{"unknown1":0,"unknown2":1,"conditionName":"noticeEvent"}],"pushWithCircleEventConditions":[{"conditionName":"pushDefault","radius":4.0,"secondaryRadius":10.0,"outwards":false,"silent":false,"isDisabled":false,"flags":1,"unknown2":0,"useSourceActorId":true}]}'
WHERE `id`=1090264;

UPDATE `server_npc_spawn_evidence`
SET `evidenceSource`='reviewed-1x-zone-layout-and-runtime-implementation',
    `evidenceReference`='The-Primal-Launcher@49169ae25b034ed65bcec9e4abdc68a6fb229f52:PrimalLauncher/Resources/xml/zones/xE9/npc.xml assigns actor class 1090264 at 160/0/-142.76; PrimalLauncher/Actors/Npc/Object/MarketEntrance.cs defines an enabled 4.0-unit inward, non-silent pushDefault circle and the shared eventPushChoiceAreaOrQuest flow; 2026-07-19 direct-core runtime observations disprove the reused 3322/321 and 4143/421 trigger-box pairs for wil0Office01',
    `confidenceStatus`='RepoConfirmed'
WHERE `evidenceId`='company-office-exit-immortal-flames';

INSERT INTO `server_npc_spawn_evidence_catalog`
(`catalogId`,`version`,`contentHashSha256`,`clientBuild`,`recordCount`)
VALUES ('zone-service-npcs-1.23b','2026.07.19.1','f40276dea0ce6739b40d0dca3dc44f665ee525646851592a9439d5013f97b8de','2012.09.19.0001',23)
ON DUPLICATE KEY UPDATE
`version`=VALUES(`version`),`contentHashSha256`=VALUES(`contentHashSha256`),
`clientBuild`=VALUES(`clientBuild`),`recordCount`=VALUES(`recordCount`);
