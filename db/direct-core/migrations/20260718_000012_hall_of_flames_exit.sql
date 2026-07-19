-- Restore the Hall of Flames exit trigger using its reviewed MarketEntrance
-- actor class and the official 0x0175 packet for that class. The prior row's
-- legacy `size` property was not a protocol field and serialized as zero IDs.

UPDATE `gamedata_actor_class`
SET `classPath`='/Chara/Npc/Object/MarketEntrance',
    `displayNameId`=0,
    `propertyFlags`=1,
    `eventConditions`='{"noticeEventConditions":[{"unknown1":0,"unknown2":1,"conditionName":"noticeEvent"}],"pushWithBoxEventConditions":[{"bgObj":3322,"layout":321,"conditionName":"in","reactName":"dtwi","silent":false,"outwards":false}]}'
WHERE `id`=1090264;

UPDATE `server_npc_spawn_evidence`
SET `evidenceSource`='official-trigger-packet-and-reviewed-1x-zone-layout',
    `evidenceReference`='ffxiv_traces/moving_around_gridania.pcapng frame:93 carries actor-class 1090264 trigger 0xCFA/0x141/in/dtwi; The-Primal-Launcher@49169ae25b034ed65bcec9e4abdc68a6fb229f52:PrimalLauncher/Resources/xml/zones/xE9/npc.xml assigns class 1090264 to the Hall exit',
    `confidenceStatus`='TraceConfirmed'
WHERE `evidenceId`='company-office-exit-immortal-flames';

INSERT INTO `server_npc_spawn_evidence_catalog`
(`catalogId`,`version`,`contentHashSha256`,`clientBuild`,`recordCount`)
VALUES ('zone-service-npcs-1.23b','2026.07.18.4','c7458280f9b1cdb1d4b483834aa1c9622638df8d9755f900960257160606dff3','2012.09.19.0001',23)
ON DUPLICATE KEY UPDATE
`version`=VALUES(`version`),`contentHashSha256`=VALUES(`contentHashSha256`),
`clientBuild`=VALUES(`clientBuild`),`recordCount`=VALUES(`recordCount`);
