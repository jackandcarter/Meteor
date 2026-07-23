-- Restore the retail ContentPrivateAreaRange actor used by escort duties.
-- The actor itself is invisible; its exit/caution push circles produce the
-- moving duty boundary on the client's minimap as Powle advances.
START TRANSACTION;

UPDATE `gamedata_actor_class`
SET `classPath`='/Chara/Npc/Object/ContentPrivateAreaRange',
    `propertyFlags`=1,
    `eventConditions`='{"talkEventConditions":[],"noticeEventConditions":[],"emoteEventConditions":[],"pushWithCircleEventConditions":[{"conditionName":"exit","radius":60.0,"secondaryRadius":60.0,"outwards":false,"silent":true,"isDisabled":false,"flags":0,"unknown2":0,"useSourceActorId":true},{"conditionName":"caution","radius":50.0,"secondaryRadius":50.0,"outwards":false,"silent":true,"isDisabled":false,"flags":0,"unknown2":0,"useSourceActorId":true}]}'
WHERE `id`=1290003;

COMMIT;
