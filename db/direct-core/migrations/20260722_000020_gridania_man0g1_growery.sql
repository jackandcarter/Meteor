-- Restore the retail Man0g1 Instance 14/15 Growery casts and emote contract.
-- Cast identities, transforms, event names, and emote command ids are taken
-- from the reviewed 1.x quest phase and instance-layout resources. The final
-- quest-owned approach volume is centered on that exact Instance 15 cast; its
-- sequential actor identity and processEvent160 handoff are corroborated by
-- the retained quest continuation and the documented objective flow.
START TRANSACTION;

INSERT INTO `gamedata_actor_class`
(`id`,`classPath`,`displayNameId`,`propertyFlags`,`eventConditions`)
VALUES
(1000237,'/Chara/Npc/Populace/PopulaceStandard',1500021,19,'{"talkEventConditions":[{"unknown1":4,"unknown2":0,"conditionName":"talkDefault"}],"noticeEventConditions":[{"unknown1":0,"unknown2":1,"conditionName":"noticeEvent"}]}'),
(1000238,'/Chara/Npc/Populace/PopulaceStandard',1000029,19,'{"talkEventConditions":[{"unknown1":4,"unknown2":0,"conditionName":"talkDefault"}],"noticeEventConditions":[{"unknown1":0,"unknown2":1,"conditionName":"noticeEvent"}],"emoteEventConditions":[{"unknown1":4,"unknown2":0,"emoteId":107,"conditionName":"emoteDefault1"}]}'),
(1000239,'/Chara/Npc/Populace/PopulaceStandard',1100025,19,'{"talkEventConditions":[{"unknown1":4,"unknown2":0,"conditionName":"talkDefault"}],"noticeEventConditions":[{"unknown1":0,"unknown2":1,"conditionName":"noticeEvent"}],"emoteEventConditions":[{"unknown1":4,"unknown2":0,"emoteId":105,"conditionName":"emoteDefault1"}]}'),
(1000409,'/Chara/Npc/Populace/PopulaceStandard',1200068,19,'{"talkEventConditions":[{"unknown1":4,"unknown2":0,"conditionName":"talkDefault"}],"noticeEventConditions":[{"unknown1":0,"unknown2":1,"conditionName":"noticeEvent"}],"emoteEventConditions":[{"unknown1":4,"unknown2":0,"emoteId":106,"conditionName":"emoteDefault1"}]}'),
(1000410,'/Chara/Npc/Populace/PopulaceStandard',1300028,19,'{"talkEventConditions":[{"unknown1":4,"unknown2":0,"conditionName":"talkDefault"}],"noticeEventConditions":[{"unknown1":0,"unknown2":1,"conditionName":"noticeEvent"}],"emoteEventConditions":[{"unknown1":4,"unknown2":0,"emoteId":108,"conditionName":"emoteDefault1"}]}'),
(1000411,'/Chara/Npc/Populace/PopulaceStandard',1100294,19,'{"talkEventConditions":[{"unknown1":4,"unknown2":0,"conditionName":"talkDefault"}],"noticeEventConditions":[{"unknown1":0,"unknown2":1,"conditionName":"noticeEvent"}],"emoteEventConditions":[{"unknown1":4,"unknown2":0,"emoteId":122,"conditionName":"emoteDefault1"}]}'),
(1000412,'/Chara/Npc/Populace/PopulaceStandard',1000414,19,'{"talkEventConditions":[{"unknown1":4,"unknown2":0,"conditionName":"talkDefault"}],"noticeEventConditions":[{"unknown1":0,"unknown2":1,"conditionName":"noticeEvent"}],"emoteEventConditions":[{"unknown1":4,"unknown2":0,"emoteId":101,"conditionName":"emoteDefault1"}]}'),
(1090201,'/Chara/Npc/Populace/PopulaceStandard',0,1,'{"pushWithCircleEventConditions":[{"conditionName":"pushDefault","radius":4.0,"secondaryRadius":4.0,"outwards":false,"silent":false,"isDisabled":true,"flags":0,"unknown2":0,"useSourceActorId":true}]}')
ON DUPLICATE KEY UPDATE
`classPath`=VALUES(`classPath`),`displayNameId`=VALUES(`displayNameId`),
`propertyFlags`=VALUES(`propertyFlags`),`eventConditions`=VALUES(`eventConditions`);

INSERT INTO `server_spawn_locations`
(`id`,`actorClassId`,`uniqueId`,`zoneId`,`privateAreaName`,`privateAreaLevel`,`positionX`,`positionY`,`positionZ`,`rotation`,`actorState`,`animationId`,`customDisplayName`)
VALUES
(1033,1090384,'man0g1_instance14_boundary',155,'PrivateAreaMasterPast',1,-223.08,12,-1498.546,-1.64,0,0,NULL),
(1034,1000237,'man0g1_instance14_fufucha',155,'PrivateAreaMasterPast',1,-232.37,12,-1500.185,1.62,0,0,NULL),
(1035,1000410,'man0g1_instance14_aunille',155,'PrivateAreaMasterPast',1,-217.52,12,-1497.75,-1.64,0,0,NULL),
(1036,1000238,'man0g1_instance14_powle',155,'PrivateAreaMasterPast',1,-219.80,12,-1495.325,-1.64,0,0,NULL),
(1037,1000239,'man0g1_instance14_sansa',155,'PrivateAreaMasterPast',1,-219.90,12,-1496.951,-1.64,0,0,NULL),
(1038,1000409,'man0g1_instance14_nicollaux',155,'PrivateAreaMasterPast',1,-220.12,12,-1492.87,-1.64,0,0,NULL),
(1039,1000412,'man0g1_instance14_ryd',155,'PrivateAreaMasterPast',1,-219.90,12,-1499.36,-1.48,0,0,NULL),
(1040,1000411,'man0g1_instance14_elyn',155,'PrivateAreaMasterPast',1,-215.96,12,-1491.787,-1.64,0,0,NULL),
(1041,1090384,'man0g1_instance15_boundary',155,'PrivateAreaMasterPast',2,-223.08,12,-1498.546,-1.64,0,0,NULL),
(1042,1000237,'man0g1_instance15_fufucha',155,'PrivateAreaMasterPast',2,-228.37,12,-1498.185,1.62,0,0,NULL),
(1043,1000410,'man0g1_instance15_aunille',155,'PrivateAreaMasterPast',2,-215.52,12,-1493.75,-2.54,0,0,NULL),
(1044,1000238,'man0g1_instance15_powle',155,'PrivateAreaMasterPast',2,-217.96,12,-1493.50,2.7,0,0,NULL),
(1045,1000239,'man0g1_instance15_sansa',155,'PrivateAreaMasterPast',2,-217.00,12,-1495.70,-0.5,0,0,NULL),
(1046,1000409,'man0g1_instance15_nicollaux',155,'PrivateAreaMasterPast',2,-216.12,12,-1495.30,-1.64,0,0,NULL),
(1047,1000412,'man0g1_instance15_ryd',155,'PrivateAreaMasterPast',2,-215.50,12,-1495.00,-1,0,0,NULL),
(1048,1000411,'man0g1_instance15_elyn',155,'PrivateAreaMasterPast',2,-216.96,12,-1492.90,-2.8,0,0,NULL),
(1049,1090201,'man0g1_instance15_children',155,'PrivateAreaMasterPast',2,-216.34,12,-1494.36,0,0,0,NULL)
ON DUPLICATE KEY UPDATE
`actorClassId`=VALUES(`actorClassId`),`uniqueId`=VALUES(`uniqueId`),`zoneId`=VALUES(`zoneId`),
`privateAreaName`=VALUES(`privateAreaName`),`privateAreaLevel`=VALUES(`privateAreaLevel`),
`positionX`=VALUES(`positionX`),`positionY`=VALUES(`positionY`),`positionZ`=VALUES(`positionZ`),
`rotation`=VALUES(`rotation`),`actorState`=VALUES(`actorState`),
`animationId`=VALUES(`animationId`),`customDisplayName`=VALUES(`customDisplayName`);

COMMIT;
