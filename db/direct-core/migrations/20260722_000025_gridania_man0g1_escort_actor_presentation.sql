-- Complete the client presentation contract for the Man0g1 escort cast.
-- The recovered actor rows already carry the retail display and appearance
-- ids, but their stripped property/event fields leave the client on its
-- uninitialised (grey, non-animating) battle-NPC path.
START TRANSACTION;

UPDATE `gamedata_actor_class`
SET `propertyFlags`=23,
    `eventConditions`='{"talkEventConditions":[],"noticeEventConditions":[{"unknown1":0,"unknown2":1,"conditionName":"noticeEvent"}],"emoteEventConditions":[],"pushWithCircleEventConditions":[]}'
WHERE `id` IN (2290009,2290010,2205603)
  AND `classPath` IN (
    '/Chara/Npc/Monster/Fighter/FighterAllyOpeningAttacker',
    '/Chara/Npc/Monster/Chigoe/ChigoeLesserStandard'
  );

-- A non-empty custom name forces displayNameId=0 on the wire. These actors
-- have native localized names, so never expose their internal script labels.
UPDATE `server_battlenpc_spawn_locations`
SET `customDisplayName`=''
WHERE `bnpcId` BETWEEN 34 AND 41;

COMMIT;
