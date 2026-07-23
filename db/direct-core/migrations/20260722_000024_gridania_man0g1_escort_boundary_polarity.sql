-- Correct the escort halo to fire only when the player crosses outward.
-- Migration 23 restored the retail actor but encoded both circles as inward
-- volumes, so they fired immediately at the duty spawn and stole the active
-- director event before questBaseRewardSeting could acknowledge it.
START TRANSACTION;

UPDATE `gamedata_actor_class`
SET `eventConditions`='{"talkEventConditions":[],"noticeEventConditions":[],"emoteEventConditions":[],"pushWithCircleEventConditions":[{"conditionName":"exit","radius":60.0,"secondaryRadius":60.0,"outwards":true,"silent":true,"isDisabled":false,"flags":0,"unknown2":0,"useSourceActorId":true},{"conditionName":"caution","radius":50.0,"secondaryRadius":50.0,"outwards":true,"silent":true,"isDisabled":false,"flags":0,"unknown2":0,"useSourceActorId":true}]}'
WHERE `id`=1290003
  AND `classPath`='/Chara/Npc/Object/ContentPrivateAreaRange';

COMMIT;
