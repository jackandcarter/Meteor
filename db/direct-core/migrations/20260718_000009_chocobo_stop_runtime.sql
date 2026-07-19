-- Restore the exact stable ChocoboStop condition contract observed in the
-- 2012.09.19.0001 client and official world captures. The capture-local word
-- at push payload offset 4 is deliberately left at the runtime fallback; all
-- stable radii, flags, status defaults, and names are pinned here.

INSERT INTO `gamedata_actor_class`
(`id`,`classPath`,`displayNameId`,`propertyFlags`,`eventConditions`)
VALUES
(1090464,'/Chara/Npc/Object/ChocoboStop',0,1,
'{"noticeEventConditions":[{"unknown1":0,"unknown2":1,"sendStatus":false,"conditionName":"noticeEvent"}],"pushWithCircleEventConditions":[{"conditionName":"pushDefault","radius":6.0,"secondaryRadius":6.0,"outwards":false,"silent":false,"isDisabled":true,"flags":0,"unknown2":3},{"conditionName":"_!pushRequest","radius":10.0,"secondaryRadius":10.0,"outwards":false,"silent":true,"isDisabled":true,"flags":0,"unknown2":0}]}')
ON DUPLICATE KEY UPDATE
`classPath`=VALUES(`classPath`),
`displayNameId`=VALUES(`displayNameId`),
`propertyFlags`=VALUES(`propertyFlags`),
`eventConditions`=VALUES(`eventConditions`);
