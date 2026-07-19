require("global");

properties = {
    permissions = 0,
    parameters = "ss",
    description =
[[
Kicks a quest-owned event, then calls a named client event on an active quest actor without changing quest state.
!questevent <questNameOrId> <eventName>
Example: !questevent Man0u1 processEvent020
]],
}

local function send(player, message)
    player:SendMessage(MESSAGE_TYPE_SYSTEM_ERROR, "[questevent] ", message);
    print("[questevent] "..message);
end

local function getActiveQuest(player, questNameOrId)
    local questId = tonumber(questNameOrId);
    if questId ~= nil then
        return player:GetQuest(questId);
    end

    return player:GetQuest(questNameOrId);
end

local function formatResult(result)
    if result == nil then
        return "nil";
    end

    return tostring(result);
end

function onTrigger(player, argc, questNameOrId, eventName)
    if not player then
        print("[questevent] player not found");
        return;
    end

    if argc ~= 2 or questNameOrId == nil or eventName == nil then
        send(player, "usage: !questevent <questNameOrId> <eventName>");
        return;
    end

    local quest = getActiveQuest(player, questNameOrId);
    if quest == nil then
        send(player, "active quest not found: "..tostring(questNameOrId));
        return;
    end

    send(player, string.format(
        "kicking %s noticeEvent for %s phase=%d flags=0x%X",
        tostring(quest.actorName),
        tostring(eventName),
        quest:GetPhase(),
        quest:GetQuestFlags()));

    kickEventContinue(player, quest, "noticeEvent", "noticeEvent");

    send(player, string.format("calling %s.%s", tostring(quest.actorName), tostring(eventName)));

    local result = callClientFunction(player, "delegateEvent", player, quest, eventName, nil, nil, nil);
    player:EndEvent();

    send(player, string.format(
        "finished %s.%s result=%s phase=%d flags=0x%X",
        tostring(quest.actorName),
        tostring(eventName),
        formatResult(result),
        quest:GetPhase(),
        quest:GetQuestFlags()));
end
