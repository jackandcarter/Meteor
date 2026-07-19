require("global");

properties = {
    permissions = 0,
    parameters = "",
    description =
[[
Prints current position, pending zone destination, active scenario quests, and NPC Linkpearl state.
!queststate
]],
}

local function send(player, message)
    player:SendMessage(MESSAGE_TYPE_SYSTEM_ERROR, "[queststate] ", message);
    print("[queststate] "..message);
end

local function getNpcLsState(isExtra, isCalling)
    if isExtra == true and isCalling == true then
        return "alert";
    elseif isCalling == true then
        return "active";
    elseif isExtra == true then
        return "inactive";
    end

    return nil;
end

function onTrigger(player, argc)
    if not player then
        print("[queststate] player not found");
        return;
    end

    local pos = player:GetPos();
    local privateArea = player.privateArea;
    if privateArea == nil or privateArea == "" then
        privateArea = "none";
    end

    send(player, string.format(
        "pos zone=%d private=%s privateType=%d destZone=%d destSpawn=%d x=%.3f y=%.3f z=%.3f rot=%.3f",
        pos[4],
        tostring(privateArea),
        player.privateAreaType or 0,
        player.destinationZone or 0,
        player.destinationSpawnType or 0,
        pos[0],
        pos[1],
        pos[2],
        pos[3]));

    local questCount = 0;
    for slot = 0, 15 do
        local quest = player.questScenario[slot];
        if quest ~= nil then
            questCount = questCount + 1;
            send(player, string.format(
                "quest slot=%d name=%s id=%d phase=%d flags=0x%X",
                slot,
                tostring(quest.actorName),
                quest:GetQuestId(),
                quest:GetPhase(),
                quest:GetQuestFlags()));
        end
    end

    if questCount == 0 then
        send(player, "no active scenario quests");
    end

    local linkpearlCount = 0;
    for npcLsId = 0, 63 do
        local state = getNpcLsState(
            player.playerWork.npcLinkshellChatExtra[npcLsId],
            player.playerWork.npcLinkshellChatCalling[npcLsId]);

        if state ~= nil then
            linkpearlCount = linkpearlCount + 1;
            send(player, string.format("npcls id=%d state=%s", npcLsId, state));
        end
    end

    if linkpearlCount == 0 then
        send(player, "no active NPC Linkpearl state");
    end
end
