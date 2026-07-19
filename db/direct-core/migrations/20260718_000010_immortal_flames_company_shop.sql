-- Restore the 1.23b Immortal Flames company shop and its Hall of Flames exit.
-- Identity comes from the 2012.09.19.0001 actor catalog; the zone/transform
-- is pinned to the reviewed 1.x zone layout for xE9 (decimal zone 233).
-- Reapplying this migration is intentionally idempotent.

INSERT INTO `gamedata_actor_class`
(`id`,`classPath`,`displayNameId`,`propertyFlags`,`eventConditions`)
VALUES
(1090264,'/Chara/Npc/Object/MarketEntrance',0,1,'{"noticeEventConditions":[{"unknown1":0,"unknown2":1,"conditionName":"noticeEvent"}],"pushWithBoxEventConditions":[{"size":4143,"outwards":0,"silent":0,"conditionName":"in"}]}'),
(1500201,'/Chara/Npc/Populace/PopulaceCompanyShop',1900184,19,'{"talkEventConditions":[{"unknown1":4,"unknown2":0,"conditionName":"talkDefault"}],"noticeEventConditions":[{"unknown1":0,"unknown2":1,"conditionName":"noticeEvent"}]}')
ON DUPLICATE KEY UPDATE
`classPath`=VALUES(`classPath`),`displayNameId`=VALUES(`displayNameId`),
`propertyFlags`=VALUES(`propertyFlags`),`eventConditions`=VALUES(`eventConditions`);

INSERT INTO `gamedata_actor_appearance` VALUES
(1090264,10999,2,0,0,0,0,0,0,0,0,0,0,0,0,0,1,1,1,0,0,0,0,0,0,0,0,0,1024,0,0,0,0,0,0,0,0,0,0,0),
(1500201,8,2,5,0,0,3,0,0,0,0,0,0,0,0,0,13,13,5,0,0,0,0,0,0,0,0,46144,46082,1024,46082,46082,0,0,0,0,0,0,0,0)
ON DUPLICATE KEY UPDATE
`base`=VALUES(`base`),`size`=VALUES(`size`),`hairStyle`=VALUES(`hairStyle`),
`hairHighlightColor`=VALUES(`hairHighlightColor`),`hairVariation`=VALUES(`hairVariation`),
`faceType`=VALUES(`faceType`),`characteristics`=VALUES(`characteristics`),
`characteristicsColor`=VALUES(`characteristicsColor`),`faceEyebrows`=VALUES(`faceEyebrows`),
`faceIrisSize`=VALUES(`faceIrisSize`),`faceEyeShape`=VALUES(`faceEyeShape`),
`faceNose`=VALUES(`faceNose`),`faceFeatures`=VALUES(`faceFeatures`),`faceMouth`=VALUES(`faceMouth`),
`ears`=VALUES(`ears`),`hairColor`=VALUES(`hairColor`),`skinColor`=VALUES(`skinColor`),
`eyeColor`=VALUES(`eyeColor`),`voice`=VALUES(`voice`),`mainHand`=VALUES(`mainHand`),
`offHand`=VALUES(`offHand`),`spMainHand`=VALUES(`spMainHand`),`spOffHand`=VALUES(`spOffHand`),
`throwing`=VALUES(`throwing`),`pack`=VALUES(`pack`),`pouch`=VALUES(`pouch`),
`head`=VALUES(`head`),`body`=VALUES(`body`),`legs`=VALUES(`legs`),`hands`=VALUES(`hands`),
`feet`=VALUES(`feet`),`waist`=VALUES(`waist`),`neck`=VALUES(`neck`),
`leftEar`=VALUES(`leftEar`),`rightEar`=VALUES(`rightEar`),`leftIndex`=VALUES(`leftIndex`),
`rightIndex`=VALUES(`rightIndex`),`leftFinger`=VALUES(`leftFinger`),
`rightFinger`=VALUES(`rightFinger`);

INSERT INTO `server_spawn_locations`
(`id`,`actorClassId`,`uniqueId`,`zoneId`,`privateAreaName`,`privateAreaLevel`,`positionX`,`positionY`,`positionZ`,`rotation`,`actorState`,`animationId`,`customDisplayName`)
VALUES
(1019,1500201,'flame_company_shop',233,'',0,169,0,-177.5,-1.5,0,0,NULL),
(1020,1090264,'hall_of_flames_exit',233,'',0,160,0,-142.76,0,0,0,NULL)
ON DUPLICATE KEY UPDATE
`actorClassId`=VALUES(`actorClassId`),`uniqueId`=VALUES(`uniqueId`),`zoneId`=VALUES(`zoneId`),
`privateAreaName`=VALUES(`privateAreaName`),`privateAreaLevel`=VALUES(`privateAreaLevel`),
`positionX`=VALUES(`positionX`),`positionY`=VALUES(`positionY`),`positionZ`=VALUES(`positionZ`),
`rotation`=VALUES(`rotation`),`actorState`=VALUES(`actorState`),
`animationId`=VALUES(`animationId`),`customDisplayName`=VALUES(`customDisplayName`);

INSERT INTO `server_npc_spawn_evidence`
(`evidenceId`,`service`,`spawnId`,`actorClassId`,`zoneId`,`privateAreaName`,`positionX`,`positionY`,`positionZ`,`rotation`,`classPath`,`appearanceId`,`evidenceSource`,`evidenceReference`,`clientBuild`,`confidenceStatus`)
VALUES
('company-shop-immortal-flames','grand-company-shop',1019,1500201,233,'',169,0,-177.5,-1.5,'/Chara/Npc/Populace/PopulaceCompanyShop',1500201,'client-actor-catalog-and-reviewed-1x-zone-layout','The-Primal-Launcher@49169ae25b034ed65bcec9e4abdc68a6fb229f52:PrimalLauncher/Resources/xml/zones/xE9/npc.xml; gamedata_actor_class:1500201; gamedata_actor_appearance:1500201','2012.09.19.0001','RepoConfirmed'),
('company-office-exit-immortal-flames','grand-company-office-exit',1020,1090264,233,'',160,0,-142.76,0,'/Chara/Npc/Object/MarketEntrance',1090264,'client-actor-catalog-and-reviewed-1x-zone-layout','The-Primal-Launcher@49169ae25b034ed65bcec9e4abdc68a6fb229f52:PrimalLauncher/Resources/xml/zones/xE9/npc.xml; gamedata_actor_class:1090264; gamedata_actor_appearance:1090264','2012.09.19.0001','RepoConfirmed')
ON DUPLICATE KEY UPDATE
`service`=VALUES(`service`),`spawnId`=VALUES(`spawnId`),`actorClassId`=VALUES(`actorClassId`),
`zoneId`=VALUES(`zoneId`),`privateAreaName`=VALUES(`privateAreaName`),
`positionX`=VALUES(`positionX`),`positionY`=VALUES(`positionY`),`positionZ`=VALUES(`positionZ`),
`rotation`=VALUES(`rotation`),`classPath`=VALUES(`classPath`),`appearanceId`=VALUES(`appearanceId`),
`evidenceSource`=VALUES(`evidenceSource`),`evidenceReference`=VALUES(`evidenceReference`),
`clientBuild`=VALUES(`clientBuild`),`confidenceStatus`=VALUES(`confidenceStatus`);

INSERT INTO `server_npc_spawn_evidence_catalog`
(`catalogId`,`version`,`contentHashSha256`,`clientBuild`,`recordCount`)
VALUES ('zone-service-npcs-1.23b','2026.07.18.2','e130e8cea9fd7d44b587ee8983ffdc1f81c756f5d47fc98646f4b309d19202f8','2012.09.19.0001',22)
ON DUPLICATE KEY UPDATE
`version`=VALUES(`version`),`contentHashSha256`=VALUES(`contentHashSha256`),
`clientBuild`=VALUES(`clientBuild`),`recordCount`=VALUES(`recordCount`);
