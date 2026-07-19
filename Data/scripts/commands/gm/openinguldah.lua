require("global");

properties = {
    permissions = 0,
    parameters = "s",
    description =
[[
Ul'dah opening debug helpers.
!openinguldah status
!openinguldah linkpearl
]],
}

local function send(player, message)
    player:SendMessage(MESSAGE_TYPE_SYSTEM_ERROR, "[openinguldah] ", message);
    print("[openinguldah] "..message);
end

local function getNpcLsState(player, npcLsId)
    local isExtra = player.playerWork.npcLinkshellChatExtra[npcLsId];
    local isCalling = player.playerWork.npcLinkshellChatCalling[npcLsId];

    if isExtra == true and isCalling == true then
        return "alert";
    elseif isCalling == true then
        return "active";
    elseif isExtra == true then
        return "inactive";
    end

    return "gone";
end

function onTrigger(player, argc, action)
    if not player then
        print("[openinguldah] player not found");
        return;
    end

    if action == "status" then
        local man0u1 = player:GetQuest("Man0u1");
        if man0u1 ~= nil then
            send(player, string.format(
                "Man0u1 active phase=%d flags=0x%X npcls0=%s",
                man0u1:GetPhase(),
                man0u1:GetQuestFlags(),
                getNpcLsState(player, 0)));
        else
            send(player, string.format("Man0u1 not active npcls0=%s", getNpcLsState(player, 0)));
        end
        return;
    end

    if action == "linkpearl" or action == "npcls" then
        if player:HasQuest(110010) == false then
            send(player, "Man0u1 is not active; NPC Linkpearl state was not changed.");
            return;
        end

        player:SetNpcLS(0, NPCLS_ALERT);
        send(player, "Adventurers' Guild NPC Linkpearl set to alert for active Man0u1.");
        return;
    end

    send(player, "unknown action. Use: !openinguldah status or !openinguldah linkpearl");
end
