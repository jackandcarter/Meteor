-- Restore the client-visible guildleve search-point contract observed in
-- party_battle_leve. This is production actor data, not trace/runtime tooling.

UPDATE `gamedata_actor_class`
SET
  `classPath` = '/Chara/Npc/Object/GuildleveSearchPoint',
  `propertyFlags` = 3,
  `eventConditions` = '{\r\n  "talkEventConditions": [\r\n    {\r\n      "unknown1": 4,\r\n      "unknown2": 0,\r\n      "conditionName": "talkDefault"\r\n    }\r\n  ],\r\n  "noticeEventConditions": [\r\n    {\r\n      "unknown1": 4,\r\n      "unknown2": 0,\r\n      "conditionName": "pushCommand"\r\n    },\r\n    {\r\n      "unknown1": 0,\r\n      "unknown2": 1,\r\n      "conditionName": "noticeEvent"\r\n    }\r\n  ],\r\n  "pushWithCircleEventConditions": [\r\n    {\r\n      "radius": "5.0",\r\n      "outwards": "false",\r\n      "silent": "true",\r\n      "conditionName": "pushCommandIn"\r\n    },\r\n    {\r\n      "radius": "5.0",\r\n      "outwards": "true",\r\n      "silent": "true",\r\n      "conditionName": "pushCommandOut"\r\n    }\r\n  ]\r\n}'
WHERE `id` = 1200036;

INSERT INTO `gamedata_actor_pushcommand`
  (`id`, `pushCommand`, `pushCommandSub`, `pushCommandPriority`)
VALUES
  (1200036, 10003, 0, 12)
ON DUPLICATE KEY UPDATE
  `pushCommand` = VALUES(`pushCommand`),
  `pushCommandSub` = VALUES(`pushCommandSub`),
  `pushCommandPriority` = VALUES(`pushCommandPriority`);
