-- Reviewed 1.23b repair/stablemaster compatibility seed.
-- Observations are curated in Data/seeds/npc-services and are never imported
-- directly from a pcap. Reapplying this migration is intentionally idempotent.

CREATE TABLE IF NOT EXISTS `server_npc_spawn_evidence_catalog` (
  `catalogId` varchar(64) NOT NULL,
  `version` varchar(32) NOT NULL,
  `contentHashSha256` char(64) NOT NULL,
  `clientBuild` varchar(32) NOT NULL,
  `recordCount` int(10) unsigned NOT NULL,
  `updatedAt` timestamp NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  PRIMARY KEY (`catalogId`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8;

CREATE TABLE IF NOT EXISTS `server_npc_spawn_evidence` (
  `evidenceId` varchar(64) NOT NULL,
  `service` varchar(32) NOT NULL,
  `spawnId` int(10) unsigned NOT NULL,
  `actorClassId` int(10) unsigned NOT NULL,
  `zoneId` int(10) unsigned NOT NULL,
  `privateAreaName` varchar(32) NOT NULL DEFAULT '',
  `positionX` float NOT NULL,
  `positionY` float NOT NULL,
  `positionZ` float NOT NULL,
  `rotation` float NOT NULL,
  `classPath` varchar(96) NOT NULL,
  `appearanceId` int(10) unsigned NOT NULL,
  `evidenceSource` varchar(64) NOT NULL,
  `evidenceReference` varchar(512) NOT NULL,
  `clientBuild` varchar(32) NOT NULL,
  `confidenceStatus` enum('RepoConfirmed','TraceConfirmed') NOT NULL,
  PRIMARY KEY (`evidenceId`),
  UNIQUE KEY `uq_service_spawn_evidence_spawn` (`spawnId`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8;

INSERT INTO `server_npc_spawn_evidence_catalog`
(`catalogId`,`version`,`contentHashSha256`,`clientBuild`,`recordCount`)
VALUES ('zone-service-npcs-1.23b','2026.07.18.1','676929e7e9961bd90c933a00af3ca7a3ba29a6f933cadcd00873f2e28e662997','2012.09.19.0001',20)
ON DUPLICATE KEY UPDATE
`version`=VALUES(`version`),`contentHashSha256`=VALUES(`contentHashSha256`),
`clientBuild`=VALUES(`clientBuild`),`recordCount`=VALUES(`recordCount`);

-- Canonical 2012.09.19.0001 actor classes required by the reviewed spawns.
INSERT INTO `gamedata_actor_class`
(`id`,`classPath`,`displayNameId`,`propertyFlags`,`eventConditions`)
VALUES
(1000840,'/Chara/Npc/Populace/PopulaceChocoboLender',1400060,19,'{"talkEventConditions":[{"unknown1":4,"unknown2":0,"conditionName":"talkDefault"}],"noticeEventConditions":[{"unknown1":0,"unknown2":1,"conditionName":"noticeEvent"}]}'),
(1080101,'/Chara/Npc/Populace/PopulaceStandard',0,1,'{}'),
(1200022,'/Chara/Npc/Populace/PopulaceStandard',0,1,'{"talkEventConditions":[],"noticeEventConditions":[{"unknown1":0,"unknown2":1,"conditionName":"noticeEvent"}]}'),
(1200044,'/Chara/Npc/Populace/PopulaceStandard',0,1,'{"noticeEventConditions":[{"unknown1":0,"unknown2":1,"conditionName":"noticeEvent"}]}'),
(1500006,'/Chara/Npc/Populace/PopulaceChocoboLender',1100197,19,'{"talkEventConditions":[{"unknown1":4,"unknown2":0,"conditionName":"talkDefault"}],"noticeEventConditions":[{"unknown1":0,"unknown2":1,"conditionName":"noticeEvent"}]}'),
(1500061,'/Chara/Npc/Populace/PopulaceChocoboLender',1600075,19,'{"talkEventConditions":[{"unknown1":4,"unknown2":0,"conditionName":"talkDefault"}],"noticeEventConditions":[{"unknown1":0,"unknown2":1,"conditionName":"noticeEvent"}]}'),
(1500114,'/Chara/Npc/Populace/PopulaceItemRepairer',1200037,19,'{"talkEventConditions":[{"unknown1":4,"unknown2":0,"conditionName":"talkDefault"}],"noticeEventConditions":[{"unknown1":0,"unknown2":1,"conditionName":"noticeEvent"}]}'),
(1500115,'/Chara/Npc/Populace/PopulaceItemRepairer',1100208,19,'{"talkEventConditions":[{"unknown1":4,"unknown2":0,"conditionName":"talkDefault"}],"noticeEventConditions":[{"unknown1":0,"unknown2":1,"conditionName":"noticeEvent"}]}'),
(1500116,'/Chara/Npc/Populace/PopulaceItemRepairer',1400116,19,'{"talkEventConditions":[{"unknown1":4,"unknown2":0,"conditionName":"talkDefault"}],"noticeEventConditions":[{"unknown1":0,"unknown2":1,"conditionName":"noticeEvent"}]}'),
(1500238,'/Chara/Npc/Populace/PopulaceItemRepairer',1000048,19,'{"talkEventConditions":[{"unknown1":4,"unknown2":0,"conditionName":"talkDefault"}],"noticeEventConditions":[{"unknown1":0,"unknown2":1,"conditionName":"noticeEvent"}]}'),
(1500252,'/Chara/Npc/Populace/PopulaceItemRepairer',1900261,19,'{"talkEventConditions":[{"unknown1":4,"unknown2":0,"conditionName":"talkDefault"}],"noticeEventConditions":[{"unknown1":0,"unknown2":1,"conditionName":"noticeEvent"}]}'),
(1500255,'/Chara/Npc/Populace/PopulaceItemRepairer',1200204,19,'{"talkEventConditions":[{"unknown1":4,"unknown2":0,"conditionName":"talkDefault"}],"noticeEventConditions":[{"unknown1":0,"unknown2":1,"conditionName":"noticeEvent"}]}'),
(1500261,'/Chara/Npc/Populace/PopulaceItemRepairer',1000099,19,'{"talkEventConditions":[{"unknown1":4,"unknown2":0,"conditionName":"talkDefault"}],"noticeEventConditions":[{"unknown1":0,"unknown2":1,"conditionName":"noticeEvent"}]}'),
(1500428,'/Chara/Npc/Populace/PopulaceItemRepairer',1600267,19,'{"talkEventConditions":[{"unknown1":4,"unknown2":0,"conditionName":"talkDefault"}],"noticeEventConditions":[{"unknown1":0,"unknown2":1,"conditionName":"noticeEvent"}]}')
ON DUPLICATE KEY UPDATE
`classPath`=VALUES(`classPath`),`displayNameId`=VALUES(`displayNameId`),
`propertyFlags`=VALUES(`propertyFlags`),`eventConditions`=VALUES(`eventConditions`);

-- Canonical appearances for the same reviewed actor set. These full rows are
-- embedded so a clean or partially damaged compatibility database converges.
INSERT INTO `gamedata_actor_appearance` VALUES
(1000840,5,2,2,0,0,1,0,0,3,0,2,3,1,1,0,16,8,8,0,0,0,0,0,0,0,0,21577,9602,4164,11329,21537,163840,0,0,0,0,0,0,0),
(1080101,10999,2,0,0,0,0,0,0,0,0,0,0,0,0,0,1,1,1,0,0,0,0,0,0,0,0,0,1024,0,0,0,0,0,0,0,0,0,0,0),
(1200022,20949,2,0,0,0,0,0,0,0,0,0,0,0,0,0,1,1,1,0,0,0,0,0,0,0,0,0,1024,0,0,0,0,0,0,0,0,0,0,0),
(1200044,10702,2,0,0,0,0,0,0,0,0,0,0,0,0,0,1,1,1,0,0,0,0,0,0,0,0,0,3074,0,0,0,0,0,0,0,0,0,0,0),
(1500006,2,2,8,0,0,4,0,0,0,0,0,0,0,0,0,13,13,15,0,0,0,0,0,0,0,0,6147,9379,5250,5188,21505,6144,0,0,0,0,0,0,0),
(1500061,7,2,1,0,0,5,0,0,3,1,4,1,2,1,3,21,11,9,0,0,0,0,0,0,0,0,4292,9601,4163,11332,21635,6144,0,0,0,0,0,0,0),
(1500114,3,2,5,0,0,7,0,0,2,0,5,4,2,0,3,26,15,15,0,0,0,0,0,0,0,0,21537,32836,2112,2114,2081,124928,0,0,0,0,0,0,0),
(1500115,2,2,8,0,0,3,0,0,4,1,4,5,2,0,2,29,16,15,0,0,0,0,0,0,0,0,25739,9378,5184,5250,10336,148480,0,0,0,0,0,0,0),
(1500116,5,2,4,0,0,1,0,0,1,1,5,1,1,1,3,3,14,8,0,0,0,0,0,0,0,0,23585,31875,2113,11360,25697,248832,0,0,0,0,0,0,0),
(1500238,1,3,2,0,0,7,0,0,4,1,0,3,3,0,0,26,2,16,0,0,0,0,0,0,0,0,35968,30852,28864,2304,25728,0,0,0,0,0,0,0,0),
(1500252,8,2,1,0,0,6,0,0,0,1,4,2,3,3,1,53,66,59,0,0,0,0,0,0,0,0,35968,30852,28739,2305,25760,0,0,0,0,0,0,0,0),
(1500255,3,2,6,0,0,3,0,0,5,0,1,0,3,1,3,28,10,7,0,0,0,0,0,0,0,0,35968,30722,28864,2304,25632,0,0,0,0,0,0,0,0),
(1500261,1,2,7,0,0,1,5,0,5,1,3,3,0,2,2,1,6,5,0,0,0,0,0,0,0,0,35968,30852,28739,2305,25760,0,0,0,0,0,0,0,0),
(1500428,7,2,6,0,0,1,0,0,0,0,0,0,0,0,0,21,14,1,0,0,0,0,0,0,0,0,21568,5185,5185,5185,21633,0,0,0,0,0,0,0,0)
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
(22,1500006,'isleen',133,'',0,-440.19,19,206.1,3.14,0,2051,NULL),
(59,1000840,'rururaji',175,'',0,-36.99,192.1,15.9,-0.56,0,1017,NULL),
(667,1500061,'fruhdhem',206,'',0,65.02,11.79,-1241.79,-2.76,0,2051,NULL),
(118,1500116,'gogorano',175,'',0,-77.82,192.1,4.29,0.23,0,1040,NULL),
(435,1500114,'braitognieux',230,'',0,-693.51,16.2,248,-2.86,0,1016,NULL),
(664,1500115,'meara',206,'',0,204.21,27.7,-1438.26,-1.5,0,1040,NULL),
(958,1500428,'repairman',180,'PrivateAreaMasterMarket',0,153.3,1,194,1.57,0,0,NULL),
(968,1500252,'mauh_lihzeh',151,'',0,1703.37,20.57,-850.32,-3.04,0,0,NULL),
(975,1500238,'beneger',154,'',0,723.07,-11.45,1127.25,1.63,0,0,NULL),
(985,1500261,'emanuel',152,'',0,-1046.89,20.5,-1773.95,-1.54,0,0,NULL),
(1016,1500255,'hortefense',129,'',0,-1893.68,53.41,-1361.25,3.12,0,0,NULL),
(966,1080101,'sys_chocoview_lim',133,'',0,-436.185,19,206.26,3.13,0,0,NULL),
(967,1080101,'sys_chocoview_uld',175,'',0,-36.611,192.037,18.759,-0.81,0,0,NULL),
(666,1080101,'sys_chocoview_grid',206,'',0,68.44,10.87,-1244.42,2.45,0,0,NULL),
(33,1200044,'chocobo',133,'',0,-441.12,19,206.39,2.59,0,0,NULL),
(34,1200022,'chocobo_standard',133,'',0,-438.76,19,207.6,3.14,0,0,NULL),
(108,1200022,'',175,'',0,-39.26,192.1,13.97,-0.38,0,0,NULL),
(111,1200044,'',175,'',0,-41.24,192.1,12.87,0.1,0,0,NULL),
(669,1200022,'chocobo_standard',206,'',0,66.73,11.73,-1242.08,-2.89,0,0,NULL),
(670,1200044,'chocobo',206,'',0,64.29,11.79,-1240.98,-2.93,0,0,NULL)
ON DUPLICATE KEY UPDATE
`actorClassId`=VALUES(`actorClassId`),`uniqueId`=VALUES(`uniqueId`),`zoneId`=VALUES(`zoneId`),
`privateAreaName`=VALUES(`privateAreaName`),`privateAreaLevel`=VALUES(`privateAreaLevel`),
`positionX`=VALUES(`positionX`),`positionY`=VALUES(`positionY`),`positionZ`=VALUES(`positionZ`),
`rotation`=VALUES(`rotation`),`actorState`=VALUES(`actorState`),`animationId`=VALUES(`animationId`),
`customDisplayName`=VALUES(`customDisplayName`);

INSERT INTO `server_npc_spawn_evidence`
(`evidenceId`,`service`,`spawnId`,`actorClassId`,`zoneId`,`privateAreaName`,`positionX`,`positionY`,`positionZ`,`rotation`,`classPath`,`appearanceId`,`evidenceSource`,`evidenceReference`,`clientBuild`,`confidenceStatus`)
VALUES
('stablemaster-isleen','stablemaster',22,1500006,133,'',-440.19,19,206.1,3.14,'/Chara/Npc/Populace/PopulaceChocoboLender',1500006,'client-and-reviewed-seed','actor-catalog:spawn:22; patch-1.19-notes','2012.09.19.0001','RepoConfirmed'),
('stablemaster-rururaji','stablemaster',59,1000840,175,'',-36.99,192.1,15.9,-0.56,'/Chara/Npc/Populace/PopulaceChocoboLender',1000840,'client-and-reviewed-seed','actor-catalog:spawn:59; patch-1.19-notes','2012.09.19.0001','RepoConfirmed'),
('stablemaster-fruhdhem','stablemaster',667,1500061,206,'',65.02,11.79,-1241.79,-2.76,'/Chara/Npc/Populace/PopulaceChocoboLender',1500061,'client-and-reviewed-seed','actor-catalog:spawn:667; patch-1.19-notes','2012.09.19.0001','RepoConfirmed'),
('repair-gogorano','repair',118,1500116,175,'',-77.82,192.1,4.29,0.23,'/Chara/Npc/Populace/PopulaceItemRepairer',1500116,'client-and-reviewed-seed','actor-catalog:spawn:118','2012.09.19.0001','RepoConfirmed'),
('repair-braitognieux','repair',435,1500114,230,'',-693.51,16.2,248,-2.86,'/Chara/Npc/Populace/PopulaceItemRepairer',1500114,'client-and-reviewed-seed','actor-catalog:spawn:435','2012.09.19.0001','RepoConfirmed'),
('repair-meara','repair',664,1500115,206,'',204.21,27.7,-1438.26,-1.5,'/Chara/Npc/Populace/PopulaceItemRepairer',1500115,'client-and-reviewed-seed','actor-catalog:spawn:664','2012.09.19.0001','RepoConfirmed'),
('repair-market','repair',958,1500428,180,'PrivateAreaMasterMarket',153.3,1,194,1.57,'/Chara/Npc/Populace/PopulaceItemRepairer',1500428,'client-and-reviewed-seed','actor-catalog:spawn:958','2012.09.19.0001','RepoConfirmed'),
('repair-mauh-lihzeh','repair',968,1500252,151,'',1703.37,20.57,-850.32,-3.04,'/Chara/Npc/Populace/PopulaceItemRepairer',1500252,'official-trace-and-client','repair_items.pcapng sha256:bbd829cbd3f42b2cbfb3aa8a74a1cc35d4e2c16c99e5a4a20ca76573f02dd90f tcp:0 frame:2','2012.09.19.0001','TraceConfirmed'),
('repair-beneger','repair',975,1500238,154,'',723.07,-11.45,1127.25,1.63,'/Chara/Npc/Populace/PopulaceItemRepairer',1500238,'client-and-reviewed-seed','actor-catalog:spawn:975','2012.09.19.0001','RepoConfirmed'),
('repair-emanuel','repair',985,1500261,152,'',-1046.89,20.5,-1773.95,-1.54,'/Chara/Npc/Populace/PopulaceItemRepairer',1500261,'client-and-reviewed-seed','actor-catalog:spawn:985','2012.09.19.0001','RepoConfirmed'),
('repair-hortefense','repair',1016,1500255,129,'',-1893.68,53.41,-1361.25,3.12,'/Chara/Npc/Populace/PopulaceItemRepairer',1500255,'client-and-reviewed-seed','actor-catalog:spawn:1016','2012.09.19.0001','RepoConfirmed'),
('chocoview-limsa','chocobo-required-actor',966,1080101,133,'',-436.185,19,206.26,3.13,'/Chara/Npc/Populace/PopulaceStandard',1080101,'client-and-reviewed-seed','actor-catalog:spawn:966','2012.09.19.0001','RepoConfirmed'),
('chocoview-uldah','chocobo-required-actor',967,1080101,175,'',-36.611,192.037,18.759,-0.81,'/Chara/Npc/Populace/PopulaceStandard',1080101,'client-and-reviewed-seed','actor-catalog:spawn:967','2012.09.19.0001','RepoConfirmed'),
('chocoview-gridania','chocobo-required-actor',666,1080101,206,'',68.44,10.87,-1244.42,2.45,'/Chara/Npc/Populace/PopulaceStandard',1080101,'client-and-reviewed-seed','actor-catalog:spawn:666','2012.09.19.0001','RepoConfirmed'),
('stable-props-limsa-chocobo','stable-prop',33,1200044,133,'',-441.12,19,206.39,2.59,'/Chara/Npc/Populace/PopulaceStandard',1200044,'client-and-reviewed-seed','actor-catalog:spawn:33','2012.09.19.0001','RepoConfirmed'),
('stable-props-limsa-standard','stable-prop',34,1200022,133,'',-438.76,19,207.6,3.14,'/Chara/Npc/Populace/PopulaceStandard',1200022,'client-and-reviewed-seed','actor-catalog:spawn:34','2012.09.19.0001','RepoConfirmed'),
('stable-props-uldah-standard','stable-prop',108,1200022,175,'',-39.26,192.1,13.97,-0.38,'/Chara/Npc/Populace/PopulaceStandard',1200022,'client-and-reviewed-seed','actor-catalog:spawn:108','2012.09.19.0001','RepoConfirmed'),
('stable-props-uldah-chocobo','stable-prop',111,1200044,175,'',-41.24,192.1,12.87,0.1,'/Chara/Npc/Populace/PopulaceStandard',1200044,'client-and-reviewed-seed','actor-catalog:spawn:111','2012.09.19.0001','RepoConfirmed'),
('stable-props-gridania-standard','stable-prop',669,1200022,206,'',66.73,11.73,-1242.08,-2.89,'/Chara/Npc/Populace/PopulaceStandard',1200022,'client-and-reviewed-seed','actor-catalog:spawn:669','2012.09.19.0001','RepoConfirmed'),
('stable-props-gridania-chocobo','stable-prop',670,1200044,206,'',64.29,11.79,-1240.98,-2.93,'/Chara/Npc/Populace/PopulaceStandard',1200044,'client-and-reviewed-seed','actor-catalog:spawn:670','2012.09.19.0001','RepoConfirmed')
ON DUPLICATE KEY UPDATE
`service`=VALUES(`service`),`spawnId`=VALUES(`spawnId`),`actorClassId`=VALUES(`actorClassId`),
`zoneId`=VALUES(`zoneId`),`privateAreaName`=VALUES(`privateAreaName`),
`positionX`=VALUES(`positionX`),`positionY`=VALUES(`positionY`),`positionZ`=VALUES(`positionZ`),
`rotation`=VALUES(`rotation`),`classPath`=VALUES(`classPath`),`appearanceId`=VALUES(`appearanceId`),
`evidenceSource`=VALUES(`evidenceSource`),`evidenceReference`=VALUES(`evidenceReference`),
`clientBuild`=VALUES(`clientBuild`),`confidenceStatus`=VALUES(`confidenceStatus`);
