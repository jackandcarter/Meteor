-- Restore the retail Man0g1 phase-15 dual-guild branch and instance-13 cast.
-- The actor and scene coordinates are the reviewed 1.x quest/zone layout.
START TRANSACTION;

INSERT INTO `server_zones_privateareas`
(`id`,`parentZoneId`,`className`,`privateAreaName`,`privateAreaType`,`dayMusic`,`nightMusic`,`battleMusic`)
VALUES
(11,155,'/Area/PrivateArea/PrivateAreaMasterPast','PrivateAreaMasterPast',0,40,0,0)
ON DUPLICATE KEY UPDATE
`parentZoneId`=VALUES(`parentZoneId`),`className`=VALUES(`className`),
`privateAreaName`=VALUES(`privateAreaName`),`privateAreaType`=VALUES(`privateAreaType`),
`dayMusic`=VALUES(`dayMusic`),`nightMusic`=VALUES(`nightMusic`),`battleMusic`=VALUES(`battleMusic`);

INSERT INTO `gamedata_actor_class`
(`id`,`classPath`,`displayNameId`,`propertyFlags`,`eventConditions`)
VALUES
(1000028,'/Chara/Npc/Populace/PopulaceStandard',1100010,19,'{"talkEventConditions":[{"unknown1":4,"unknown2":0,"conditionName":"talkDefault"}],"noticeEventConditions":[{"unknown1":0,"unknown2":1,"conditionName":"noticeEvent"}]}'),
(1000033,'/Chara/Npc/Populace/PopulaceStandard',2000010,19,'{"talkEventConditions":[{"unknown1":4,"unknown2":0,"conditionName":"talkDefault"}],"noticeEventConditions":[{"unknown1":0,"unknown2":1,"conditionName":"noticeEvent"}]}'),
(1000372,'/Chara/Npc/Populace/PopulaceStandard',1000141,19,'{"talkEventConditions":[{"unknown1":4,"unknown2":0,"conditionName":"talkDefault"}],"noticeEventConditions":[{"unknown1":0,"unknown2":1,"conditionName":"noticeEvent"}]}'),
(1000460,'/Chara/Npc/Populace/PopulaceStandard',2200149,19,'{"talkEventConditions":[{"unknown1":4,"unknown2":0,"conditionName":"talkDefault"}],"noticeEventConditions":[{"unknown1":0,"unknown2":1,"conditionName":"noticeEvent"}]}'),
(1000513,'/Chara/Npc/Populace/PopulaceStandard',1500077,19,'{"talkEventConditions":[{"unknown1":4,"unknown2":0,"conditionName":"talkDefault"}],"noticeEventConditions":[{"unknown1":0,"unknown2":1,"conditionName":"noticeEvent"}]}'),
(1000737,'/Chara/Npc/Populace/PopulaceStandard',1100121,19,'{"talkEventConditions":[{"unknown1":4,"unknown2":0,"conditionName":"talkDefault"}],"noticeEventConditions":[{"unknown1":0,"unknown2":1,"conditionName":"noticeEvent"}]}'),
(1001072,'/Chara/Npc/Populace/PopulaceStandard',1400050,19,'{"talkEventConditions":[{"unknown1":4,"unknown2":0,"conditionName":"talkDefault"}],"noticeEventConditions":[{"unknown1":0,"unknown2":1,"conditionName":"noticeEvent"}]}'),
(1700030,'/Chara/Npc/Populace/PopulaceStandard',1300064,19,'{"talkEventConditions":[{"unknown1":4,"unknown2":0,"conditionName":"talkDefault"}],"noticeEventConditions":[{"unknown1":0,"unknown2":1,"conditionName":"noticeEvent"}]}')
ON DUPLICATE KEY UPDATE
`classPath`=VALUES(`classPath`),`displayNameId`=VALUES(`displayNameId`),
`propertyFlags`=VALUES(`propertyFlags`),`eventConditions`=VALUES(`eventConditions`);

INSERT INTO `server_spawn_locations`
(`id`,`actorClassId`,`uniqueId`,`zoneId`,`privateAreaName`,`privateAreaLevel`,`positionX`,`positionY`,`positionZ`,`rotation`,`actorState`,`animationId`,`customDisplayName`)
VALUES
(1021,1700030,'soileine_man0g1',206,'',0,-330.92,8,-1682.7,0,0,0,NULL),
(1022,1099046,'conjurers_guild_scene_entry',206,'',0,-340.62,8.09,-1684.69,1.17,0,0,NULL),
(1023,1000009,'man0g1_yda',155,'PrivateAreaMasterPast',0,-350.5,6.24,-1693.2,-2.2,0,0,NULL),
(1024,1000010,'man0g1_papalymo',155,'PrivateAreaMasterPast',0,-351.5,6.24,-1692.6,-2.9,0,0,NULL),
(1025,1000033,'man0g1_o_app_pesi',155,'PrivateAreaMasterPast',0,-351.5,6.24,-1694.2,0.2,0,0,NULL),
(1026,1700030,'man0g1_soileine',155,'PrivateAreaMasterPast',0,-330.92,8,-1682.7,0,0,0,NULL),
(1027,1000460,'man0g1_hetzkin',155,'PrivateAreaMasterPast',0,-329,8,-1682.7,0,0,0,NULL),
(1028,1000513,'man0g1_gugula',155,'PrivateAreaMasterPast',0,-355.5,6.82,-1686.3,2.8,0,0,NULL),
(1029,1000737,'man0g1_biddy',155,'PrivateAreaMasterPast',0,-342.09,8,-1674.76,1.31,0,0,NULL),
(1030,1000028,'man0g1_swethyna',155,'PrivateAreaMasterPast',0,-352.5,6.24,-1694,1,0,0,NULL),
(1031,1000372,'man0g1_ingram',155,'PrivateAreaMasterPast',0,-333.3,8,-1669.6,2.99,0,0,NULL),
(1032,1001072,'man0g1_challinie',155,'PrivateAreaMasterPast',0,-325.62,8,-1676.6,-1.55,0,0,NULL)
ON DUPLICATE KEY UPDATE
`actorClassId`=VALUES(`actorClassId`),`uniqueId`=VALUES(`uniqueId`),`zoneId`=VALUES(`zoneId`),
`privateAreaName`=VALUES(`privateAreaName`),`privateAreaLevel`=VALUES(`privateAreaLevel`),
`positionX`=VALUES(`positionX`),`positionY`=VALUES(`positionY`),`positionZ`=VALUES(`positionZ`),
`rotation`=VALUES(`rotation`),`actorState`=VALUES(`actorState`),
`animationId`=VALUES(`animationId`),`customDisplayName`=VALUES(`customDisplayName`);

COMMIT;
