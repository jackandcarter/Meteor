-- Complete the remaining Man0g1 runtime contract: the White Wolf Gate
-- escort, Lifemend Stump scenes, guild return, and final reward handoff.
START TRANSACTION;

INSERT INTO `server_zones_privateareas`
(`id`,`parentZoneId`,`className`,`privateAreaName`,`privateAreaType`,`dayMusic`,`nightMusic`,`battleMusic`)
VALUES
(12,155,'/Area/PrivateArea/PrivateAreaMasterPast','PrivateAreaMasterPast',3,51,0,0),
(13,155,'/Area/PrivateArea/PrivateAreaMasterPast','PrivateAreaMasterPast',4,51,0,0),
(14,150,'/Area/PrivateArea/PrivateAreaMasterPast','PrivateAreaMasterPast',0,52,52,13),
(15,150,'/Area/PrivateArea/PrivateAreaMasterPast','PrivateAreaMasterPast',1,52,52,13)
ON DUPLICATE KEY UPDATE
`parentZoneId`=VALUES(`parentZoneId`),`className`=VALUES(`className`),
`privateAreaName`=VALUES(`privateAreaName`),`privateAreaType`=VALUES(`privateAreaType`),
`dayMusic`=VALUES(`dayMusic`),`nightMusic`=VALUES(`nightMusic`),`battleMusic`=VALUES(`battleMusic`);

INSERT INTO `gamedata_actor_class`
(`id`,`classPath`,`displayNameId`,`propertyFlags`,`eventConditions`)
VALUES
(1090202,'/Chara/Npc/Populace/PopulaceStandard',0,1,'{"pushWithCircleEventConditions":[{"conditionName":"pushDefault","radius":25.0,"secondaryRadius":25.0,"outwards":false,"silent":false,"isDisabled":true,"flags":0,"unknown2":0,"useSourceActorId":true}]}'),
(1090203,'/Chara/Npc/Populace/PopulaceStandard',0,1,'{"pushWithCircleEventConditions":[{"conditionName":"pushDefault","radius":12.0,"secondaryRadius":12.0,"outwards":false,"silent":false,"isDisabled":true,"flags":0,"unknown2":0,"useSourceActorId":true}]}'),
(1090204,'/Chara/Npc/Populace/PopulaceStandard',0,1,'{"pushWithCircleEventConditions":[{"conditionName":"pushDefault","radius":15.0,"secondaryRadius":15.0,"outwards":true,"silent":false,"isDisabled":true,"flags":0,"unknown2":0,"useSourceActorId":true}]}')
ON DUPLICATE KEY UPDATE
`classPath`=VALUES(`classPath`),`displayNameId`=VALUES(`displayNameId`),
`propertyFlags`=VALUES(`propertyFlags`),`eventConditions`=VALUES(`eventConditions`);

UPDATE `gamedata_actor_class`
SET `classPath`='/Chara/Npc/Monster/Fighter/FighterAllyOpeningAttacker'
WHERE `id` IN (2290009,2290010);

UPDATE `gamedata_actor_class`
SET `classPath`='/Chara/Npc/Monster/Chigoe/ChigoeLesserStandard'
WHERE `id`=2205603;

UPDATE `gamedata_actor_class`
SET `propertyFlags`=19,
    `eventConditions`='{"talkEventConditions":[{"unknown1":4,"unknown2":0,"conditionName":"talkDefault"}],"noticeEventConditions":[{"unknown1":0,"unknown2":1,"conditionName":"noticeEvent"}],"pushWithCircleEventConditions":[{"conditionName":"pushDefault","radius":12.0,"secondaryRadius":12.0,"outwards":false,"silent":false,"isDisabled":true,"flags":0,"unknown2":0,"useSourceActorId":true}]}'
WHERE `id`=1090046;

INSERT INTO `server_spawn_locations`
(`id`,`actorClassId`,`uniqueId`,`zoneId`,`privateAreaName`,`privateAreaLevel`,`positionX`,`positionY`,`positionZ`,`rotation`,`actorState`,`animationId`,`customDisplayName`)
VALUES
(1050,1090202,'man0g1_white_wolf_gate',155,'',0,-194.73,3.54,-1021.33,-1.642,0,0,NULL),
(1051,1090203,'man0g1_lifemend_stump',150,'PrivateAreaMasterPast',0,-770.197,23,-1086.209,0,0,0,NULL),
(1052,1090204,'man0g1_lifemend_exit',150,'PrivateAreaMasterPast',1,-756.77,22.77,-1092.33,0,0,0,NULL),
(1053,1000243,'man0g1_burchard_scene',155,'PrivateAreaMasterPast',3,194.4514,27.90045,-1577.0281,3.1395,0,1041,NULL),
(1054,1000681,'man0g1_nuala_scene',155,'PrivateAreaMasterPast',4,195.69795,27.90045,-1577.4851,-2.7279,0,1017,NULL),
(1055,1000243,'man0g1_burchard_scene_ending',155,'PrivateAreaMasterPast',4,194.4514,27.90045,-1577.0281,3.1395,0,1041,NULL)
ON DUPLICATE KEY UPDATE
`actorClassId`=VALUES(`actorClassId`),`uniqueId`=VALUES(`uniqueId`),`zoneId`=VALUES(`zoneId`),
`privateAreaName`=VALUES(`privateAreaName`),`privateAreaLevel`=VALUES(`privateAreaLevel`),
`positionX`=VALUES(`positionX`),`positionY`=VALUES(`positionY`),`positionZ`=VALUES(`positionZ`),
`rotation`=VALUES(`rotation`),`actorState`=VALUES(`actorState`),
`animationId`=VALUES(`animationId`),`customDisplayName`=VALUES(`customDisplayName`);

INSERT INTO `server_battlenpc_pools`
(`poolId`,`actorClassId`,`name`,`genusId`,`currentJob`,`combatSkill`,`combatDelay`,`combatDmgMult`,`aggroType`,`immunity`,`linkType`,`spellListId`,`skillListId`)
VALUES
(120,2290009,'escort_powle',29,2,1,4200,1,0,0,0,0,0),
(121,2290010,'escort_sansa',29,2,1,4200,1,0,0,0,0,0),
(122,2205603,'ankle_biter',36,0,1,4200,1,0,0,0,0,0)
ON DUPLICATE KEY UPDATE
`actorClassId`=VALUES(`actorClassId`),`name`=VALUES(`name`),`genusId`=VALUES(`genusId`),
`currentJob`=VALUES(`currentJob`),`combatSkill`=VALUES(`combatSkill`),
`combatDelay`=VALUES(`combatDelay`),`combatDmgMult`=VALUES(`combatDmgMult`),
`aggroType`=VALUES(`aggroType`),`immunity`=VALUES(`immunity`),`linkType`=VALUES(`linkType`),
`spellListId`=VALUES(`spellListId`),`skillListId`=VALUES(`skillListId`);

INSERT INTO `server_battlenpc_groups`
(`groupId`,`poolId`,`scriptName`,`minLevel`,`maxLevel`,`respawnTime`,`hp`,`mp`,`dropListId`,`allegiance`,`spawnType`,`animationId`,`actorState`,`privateAreaName`,`privateAreaLevel`,`zoneId`)
VALUES
(120,120,'escort_powle',1,1,0,200,0,0,1,1,0,0,'',0,150),
(121,121,'escort_sansa',1,1,0,200,0,0,1,1,0,0,'',0,150),
(122,122,'ankle_biter_gridania',5,7,0,60,0,0,0,1,0,0,'',0,150)
ON DUPLICATE KEY UPDATE
`poolId`=VALUES(`poolId`),`scriptName`=VALUES(`scriptName`),
`minLevel`=VALUES(`minLevel`),`maxLevel`=VALUES(`maxLevel`),`respawnTime`=VALUES(`respawnTime`),
`hp`=VALUES(`hp`),`mp`=VALUES(`mp`),`dropListId`=VALUES(`dropListId`),
`allegiance`=VALUES(`allegiance`),`spawnType`=VALUES(`spawnType`),
`animationId`=VALUES(`animationId`),`actorState`=VALUES(`actorState`),
`privateAreaName`=VALUES(`privateAreaName`),`privateAreaLevel`=VALUES(`privateAreaLevel`),`zoneId`=VALUES(`zoneId`);

INSERT INTO `server_battlenpc_spawn_locations`
(`bnpcId`,`customDisplayName`,`groupId`,`positionX`,`positionY`,`positionZ`,`rotation`)
VALUES
(34,'escort_powle',120,-197.7,3.54,-1023.3,-1.642),
(35,'escort_sansa',121,-191.7,3.54,-1023.3,-1.642),
(36,'ankle_biter',122,-183.06,3.25,-884.72,0),
(37,'ankle_biter',122,-255.57,4.12,-832.77,0),
(38,'ankle_biter',122,-376.15,4.41,-886.16,0),
(39,'ankle_biter',122,-507.36,6.92,-902.53,0),
(40,'ankle_biter',122,-619.96,3.60,-954.60,0),
(41,'ankle_biter',122,-634.94,21.94,-1070.57,0)
ON DUPLICATE KEY UPDATE
`customDisplayName`=VALUES(`customDisplayName`),`groupId`=VALUES(`groupId`),
`positionX`=VALUES(`positionX`),`positionY`=VALUES(`positionY`),
`positionZ`=VALUES(`positionZ`),`rotation`=VALUES(`rotation`);

COMMIT;
